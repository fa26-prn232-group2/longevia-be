using System.Net.Http.Json;
using Longevia.Shared.Events;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Progress.Infrastructure.Messaging;

namespace Progress.IntegrationTests.Messaging;

// Synthetic publisher + actual Progress consumer/transport/PG; not Diet producer E2E.
public sealed class RabbitMealTests(RabbitMealFixture fixture) : IClassFixture<RabbitMealFixture>
{
    private static MealLoggedEvent Event()
    {
        var id = Guid.NewGuid();
        var instant = DateTimeOffset.Parse("2026-10-25T00:00:00Z");
        return new(id, Guid.NewGuid(), id, new DateOnly(2026, 1, 15), instant, instant);
    }

    private Task PublishAsync(MealLoggedEvent message, string key = "diet.meal.logged") =>
        fixture.Bus.Publish(message, c => c.SetRoutingKey(key));

    private async Task PersistedAsync(MealLoggedEvent message)
    {
        await RabbitMealFixture.EventuallyAsync(async () =>
        {
            await using var db = fixture.Db();
            return await db.ProcessedMealEvents.AnyAsync(x => x.EventId == message.EventId);
        });
        await using var verify = fixture.Db();
        var inbox = await verify.ProcessedMealEvents.SingleAsync(x => x.EventId == message.EventId);
        var contribution = await verify.ActivityContributions.SingleAsync(x => x.Source == "Diet.Meal" && x.SourceLogId == message.MealLogId.ToString());
        Assert.Equal(message.UserId, contribution.UserId);
        Assert.Equal(message.BusinessDate, contribution.Date);
        Assert.Equal(message.MealLogId, inbox.MealLogId);
        Assert.Equal(message.BusinessDate, inbox.BusinessDate);
    }

    [Fact]
    public async Task Broker_CanonicalRouting_PersistsPairAndPreservesBackdate()
    {
        var exchange = await fixture.GetAsync("exchanges/%2F/longevia.events");
        Assert.Equal("topic", exchange.GetProperty("type").GetString());
        var queue = await fixture.GetAsync("queues/%2F/progress.meal-activity");
        Assert.True(queue.GetProperty("durable").GetBoolean());
        var bindings = await fixture.GetAsync("exchanges/%2F/longevia.events/bindings/source");
        Assert.Contains(bindings.EnumerateArray(), x => x.GetProperty("routing_key").GetString() == "diet.meal.logged"
            && x.GetProperty("destination").GetString() == "progress.meal-activity");
        var message = Event();
        await PublishAsync(message);
        await PersistedAsync(message);
        await RabbitMealFixture.EventuallyAsync(() => Task.FromResult(fixture.Observer.Completed.ContainsKey(message.EventId)));
        Assert.Equal("longevia.events", fixture.Observer.Received[message.EventId].Exchange);
        Assert.Equal("diet.meal.logged", fixture.Observer.Received[message.EventId].Key);
    }

    [Fact]
    public async Task Broker_WrongRoutingKey_IsNotProjected()
    {
        var wrong = Event();
        await PublishAsync(wrong, "diet.meal.wrong");
        // A correctly routed sentinel proves the same ordered publisher connection is drained.
        var sentinel = Event();
        await PublishAsync(sentinel);
        await PersistedAsync(sentinel);
        await using var db = fixture.Db();
        Assert.False(await db.ProcessedMealEvents.AnyAsync(x => x.EventId == wrong.EventId));
    }

    [Fact]
    public async Task Broker_ConcurrentDuplicatesAndRestart_PreserveSingleEffect()
    {
        var message = Event();
        await Task.WhenAll(Enumerable.Range(0, 12).Select(_ => PublishAsync(message)));
        await PersistedAsync(message);
        await fixture.Bus.StopAsync();
        await fixture.Bus.StartAsync();
        await PublishAsync(message);
        var sentinel = Event();
        await PublishAsync(sentinel);
        await PersistedAsync(sentinel);
        await using var db = fixture.Db();
        Assert.Equal(1, await db.ActivityContributions.CountAsync(x => x.SourceLogId == message.MealLogId.ToString()));
        Assert.Equal(1, await db.ProcessedMealEvents.CountAsync(x => x.EventId == message.EventId));
    }

    [Fact]
    public async Task Broker_TwoMealsSameDateAndDifferentUsers_IsolateActiveDays()
    {
        var a = Event();
        var b = Event() with { UserId = a.UserId };
        var other = Event();
        await PublishAsync(a); await PublishAsync(b); await PublishAsync(other);
        await PersistedAsync(a); await PersistedAsync(b); await PersistedAsync(other);
        await using var db = fixture.Db();
        Assert.Single(await db.ActivityContributions.Where(x => x.UserId == a.UserId).Select(x => x.Date).Distinct().ToListAsync());
        Assert.Equal(2, await db.ActivityContributions.CountAsync(x => x.UserId == a.UserId));
        Assert.Equal(1, await db.ActivityContributions.CountAsync(x => x.UserId == other.UserId));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Broker_PoisonOrConflict_ExhaustedRetryEntersMassTransitErrorQueue(bool conflict)
    {
        var message = Event();
        if (conflict) { await PublishAsync(message); await PersistedAsync(message); }
        var bad = conflict ? message with { UserId = Guid.NewGuid() } : message with { EventId = Guid.NewGuid() };
        await PublishAsync(bad);
        await RabbitMealFixture.EventuallyAsync(async () =>
        {
            using var response = await fixture.Management.PostAsJsonAsync("queues/%2F/progress.meal-activity_error/get",
                new { count = 50, ackmode = "ack_requeue_true", encoding = "auto", truncate = 100000 });
            if (!response.IsSuccessStatusCode) return false;
            using var json = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            return json.RootElement.EnumerateArray().Any(x => x.GetProperty("payload").GetString()!.Contains(bad.EventId.ToString()));
        });
        using var read = await fixture.Management.PostAsJsonAsync("queues/%2F/progress.meal-activity_error/get",
            new { count = 50, ackmode = "ack_requeue_true", encoding = "auto", truncate = 100000 });
        read.EnsureSuccessStatusCode();
        using var errors = System.Text.Json.JsonDocument.Parse(await read.Content.ReadAsStringAsync());
        var error = Assert.Single(errors.RootElement.EnumerateArray(), x => x.GetProperty("payload").GetString()!.Contains(bad.EventId.ToString()));
        Assert.Equal("3", error.GetProperty("properties").GetProperty("headers").GetProperty(MessageHeaders.FaultRetryCount).ToString());
        await using var db = fixture.Db();
        Assert.Equal(conflict ? 1 : 0, await db.ActivityContributions.CountAsync(x => x.SourceLogId == message.MealLogId.ToString()));
        if (conflict) Assert.Equal(message.UserId, (await db.ProcessedMealEvents.SingleAsync(x => x.EventId == message.EventId)).UserId);
    }

    [Fact]
    public async Task Broker_InterruptedUnackedConsumer_RedeliversAfterDatabaseResumes()
    {
        var message = Event();
        await fixture.Bus.StopAsync();
        using var child = fixture.StartConsumerProcess();
        var paused = false;
        try
        {
            await RabbitMealFixture.EventuallyAsync(async () =>
            {
                var queue = await fixture.GetAsync("queues/%2F/progress.meal-activity");
                return queue.TryGetProperty("consumers", out var consumers) && consumers.GetInt32() == 1;
            });
            await fixture.Database.PauseAsync();
            paused = true;
            using var sent = await fixture.Management.PostAsJsonAsync("exchanges/%2F/longevia.events/publish", new
            {
                properties = new { content_type = "application/json", delivery_mode = 2 },
                routing_key = "diet.meal.logged",
                payload = System.Text.Json.JsonSerializer.Serialize(new { messageType = new[] { "urn:message:Longevia.Shared.Events:MealLoggedEvent" }, message }),
                payload_encoding = "string"
            });
            sent.EnsureSuccessStatusCode();
            await RabbitMealFixture.EventuallyAsync(async () =>
            {
                var queue = await fixture.GetAsync("queues/%2F/progress.meal-activity");
                return queue.TryGetProperty("messages_unacknowledged", out var unacked) && unacked.GetInt32() > 0;
            });
            child.Kill(entireProcessTree: true);
            await child.WaitForExitAsync();
        }
        finally
        {
            if (!child.HasExited) { child.Kill(entireProcessTree: true); await child.WaitForExitAsync(); }
            if (paused) await fixture.Database.UnpauseAsync();
        }
        await fixture.Bus.StartAsync();
        await PersistedAsync(message);
        await RabbitMealFixture.EventuallyAsync(() => Task.FromResult(fixture.Observer.Completed.ContainsKey(message.EventId)));
        Assert.True(fixture.Observer.Received[message.EventId].Redelivered);
        await using var db = fixture.Db();
        Assert.Equal(1, await db.ActivityContributions.CountAsync(x => x.SourceLogId == message.MealLogId.ToString()));
    }

    [Fact]
    public async Task Broker_StopAndResume_PreservesDurableQueuedDelivery()
    {
        var message = Event();
        await fixture.Bus.StopAsync();
        using (var sent = await fixture.Management.PostAsJsonAsync("exchanges/%2F/longevia.events/publish", new
        {
            properties = new { content_type = "application/json", delivery_mode = 2 },
            routing_key = "diet.meal.logged",
            payload = System.Text.Json.JsonSerializer.Serialize(new { messageType = new[] { "urn:message:Longevia.Shared.Events:MealLoggedEvent" }, message }),
            payload_encoding = "string"
        })) sent.EnsureSuccessStatusCode();
        var stopped = await fixture.Rabbit.ExecAsync(["rabbitmqctl", "stop_app"]);
        Assert.Equal(0, stopped.ExitCode);
        var started = await fixture.Rabbit.ExecAsync(["rabbitmqctl", "start_app"]);
        Assert.Equal(0, started.ExitCode);
        await fixture.Bus.StartAsync();
        await PersistedAsync(message);
    }

    [Fact]
    public async Task Broker_DatabaseUnavailable_ExhaustsRetriesWithoutEffect_ThenRecovers()
    {
        var message = Event();
        await fixture.Database.PauseAsync();
        try
        {
            await PublishAsync(message);
            await RabbitMealFixture.EventuallyAsync(async () =>
            {
                using var response = await fixture.Management.PostAsJsonAsync("queues/%2F/progress.meal-activity_error/get",
                    new { count = 100, ackmode = "ack_requeue_true", encoding = "auto", truncate = 100000 });
                if (!response.IsSuccessStatusCode) return false;
                using var json = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                var matching = json.RootElement.EnumerateArray().Where(x => x.GetProperty("payload").GetString()!.Contains(message.EventId.ToString())).ToArray();
                if (matching.Length == 0) return false;
                Assert.Equal("3", Assert.Single(matching).GetProperty("properties").GetProperty("headers").GetProperty(MessageHeaders.FaultRetryCount).ToString());
                return true;
            }, 60);
            Assert.False(fixture.Observer.Completed.ContainsKey(message.EventId));
        }
        finally { await fixture.Database.UnpauseAsync(); }
        await using (var db = fixture.Db())
        {
            Assert.False(await db.ProcessedMealEvents.AnyAsync(x => x.EventId == message.EventId));
            Assert.False(await db.ActivityContributions.AnyAsync(x => x.SourceLogId == message.MealLogId.ToString()));
        }
        await PublishAsync(message);
        await PersistedAsync(message);
    }
}
