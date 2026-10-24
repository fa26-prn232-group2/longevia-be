using System.Net;
using System.Net.Http.Json;
using System.Threading.Channels;
using Diet.Application.Abstractions;
using Diet.Application.Features.Meals;
using Diet.Domain.Entities;
using Diet.Infrastructure.Messaging;
using Diet.Infrastructure.Persistence;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Longevia.Shared.Events;
using MassTransit;
using MassTransit.EntityFrameworkCoreIntegration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Diet.IntegrationTests;

public sealed class MealActivityTests : IAsyncLifetime
{
    private readonly IContainer _rabbit = new ContainerBuilder("rabbitmq:4-management")
        .WithEnvironment("RABBITMQ_DEFAULT_USER", "diet_test")
        .WithEnvironment("RABBITMQ_DEFAULT_PASS", "diet_test_password")
        .WithPortBinding(5672, true)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilMessageIsLogged("Server startup complete"))
        .Build();
    private readonly Channel<MealLoggedEvent> _received = Channel.CreateUnbounded<MealLoggedEvent>();
    private readonly DietApiFactory _factory = new();
    private IBusControl? _receiver;

    public async Task InitializeAsync()
    {
        await _rabbit.StartAsync();
        _factory.RabbitHost = _rabbit.Hostname;
        _factory.RabbitPort = _rabbit.GetMappedPublicPort(5672);
        _receiver = Bus.Factory.CreateUsingRabbitMq(bus =>
        {
            bus.Host(_rabbit.Hostname, _rabbit.GetMappedPublicPort(5672), "/", host =>
            {
                host.Username("diet_test");
                host.Password("diet_test_password");
            });
            bus.ReceiveEndpoint("diet-contract-test-" + Guid.NewGuid().ToString("N"), queue =>
            {
                queue.Durable = true;
                queue.AutoDelete = false;
                queue.ConfigureConsumeTopology = false;
                queue.Bind(DietMessagingExtensions.Exchange, binding =>
                {
                    binding.ExchangeType = "topic";
                    binding.RoutingKey = DietMessagingExtensions.RoutingKey;
                });
                queue.Handler<MealLoggedEvent>(context => _received.Writer.WriteAsync(context.Message).AsTask());
            });
        });
        await _receiver.StartAsync();
        await _factory.InitializeAsync();
    }

    public async Task DisposeAsync()
    {
        await _factory.DisposeAsync();
        if (_receiver is not null)
        {
            await _receiver.StopAsync();
        }
        await _rabbit.DisposeAsync();
    }

    [Fact]
    public async Task LogMeal_CommitsOutbox_DeliversBusinessDate_AndReplayKeepsIdentity()
    {
        var userId = Guid.NewGuid();
        using var client = _factory.UserClient(userId);
        var day = new DateOnly(2026, 9, 1);
        var response = await client.PostAsJsonAsync("/meals", new LogMealRequest(day, "Lunch",
            [new(Guid.Parse("11111111-1111-4111-8111-111111111111"), 150)]));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var meal = (await response.Content.ReadFromJsonAsync<MealResponse>())!;
        var activity = await ReadAsync();
        Assert.Equal(meal.Id, activity.EventId);
        Assert.Equal(meal.Id, activity.MealLogId);
        Assert.Equal(userId, activity.UserId);
        Assert.Equal(day, activity.BusinessDate);
        Assert.True(activity.OccurredOn >= activity.CreatedAt);
        Assert.NotEqual(DateOnly.FromDateTime(activity.CreatedAt.UtcDateTime), activity.BusinessDate);
        Assert.Equal(1, MealLoggedEvent.SchemaVersion);
        Assert.Equal(6, typeof(MealLoggedEvent).GetProperties().Length);

        await using var scope = _factory.Services.CreateAsyncScope();
        Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<MealActivityReplay>().EnqueueAsync());
        var replay = await ReadAsync();
        Assert.Equal(activity.EventId, replay.EventId);
        Assert.Equal(activity.CreatedAt, replay.CreatedAt);
        Assert.Equal(activity.BusinessDate, replay.BusinessDate);
        Assert.True(replay.OccurredOn >= activity.OccurredOn);
    }

    [Fact]
    public async Task RolledBackMeal_RemovesMealAndOutbox_AndNeverReachesBroker()
    {
        using var client = _factory.UserClient(Guid.NewGuid());
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<DietDbContext>();
        var id = Guid.NewGuid();
        var meal = MealLog.Create(id, new DateOnly(2026, 9, 2), "Dinner", DateTimeOffset.UtcNow);
        await using (var transaction = await db.Database.BeginTransactionAsync())
        {
            db.MealLogs.Add(meal);
            await scope.ServiceProvider.GetRequiredService<IMealActivityPublisher>().PublishAsync(meal, default);
            await db.SaveChangesAsync();
            Assert.True(await db.Set<OutboxMessage>().AnyAsync());
            await transaction.RollbackAsync();
        }
        db.ChangeTracker.Clear();
        Assert.False(await db.MealLogs.AnyAsync(x => x.Id == meal.Id));
        Assert.False(await db.Set<OutboxMessage>().AnyAsync());
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await _received.Reader.ReadAsync(timeout.Token));
    }

    [Fact]
    public async Task Replay_QueuesLegacyMealsAcrossBatchBoundary_WithoutLosingBusinessDates()
    {
        using var client = _factory.UserClient(Guid.NewGuid());
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<DietDbContext>();
        var userId = Guid.NewGuid();
        var day = new DateOnly(2026, 8, 1);
        var logs = Enumerable.Range(0, 101).Select(_ =>
            MealLog.Create(userId, day, "Breakfast", DateTimeOffset.UtcNow)).ToList();
        db.MealLogs.AddRange(logs);
        await db.SaveChangesAsync();
        Assert.Equal(101, await scope.ServiceProvider.GetRequiredService<MealActivityReplay>().EnqueueAsync());
        var ids = new HashSet<Guid>();
        for (var i = 0; i < 101; i++)
        {
            var activity = await ReadAsync();
            Assert.Equal(userId, activity.UserId);
            Assert.Equal(day, activity.BusinessDate);
            Assert.Equal(activity.MealLogId, activity.EventId);
            Assert.True(ids.Add(activity.EventId));
        }
        Assert.True(ids.SetEquals(logs.Select(x => x.Id)));
    }

    [Fact]
    public async Task BrokerOutage_MealCommitsToOutbox_AndDeliversAfterReconnect()
    {
        using var client = _factory.UserClient(Guid.NewGuid());
        var stopped = await _rabbit.ExecAsync(["rabbitmqctl", "stop_app"]);
        Assert.Equal(0, stopped.ExitCode);
        var day = new DateOnly(2026, 7, 1);
        var response = await client.PostAsJsonAsync("/meals", new LogMealRequest(day, "Lunch",
            [new(Guid.Parse("11111111-1111-4111-8111-111111111111"), 100)]));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var meal = (await response.Content.ReadFromJsonAsync<MealResponse>())!;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DietDbContext>();
            Assert.True(await db.MealLogs.AnyAsync(x => x.Id == meal.Id));
            Assert.True(await db.Set<OutboxMessage>().AnyAsync(x => x.MessageId == meal.Id));
        }
        var started = await _rabbit.ExecAsync(["rabbitmqctl", "start_app"]);
        Assert.Equal(0, started.ExitCode);
        var activity = await ReadAsync();
        Assert.Equal(meal.Id, activity.MealLogId);
        Assert.Equal(day, activity.BusinessDate);
    }

    private async Task<MealLoggedEvent> ReadAsync()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        return await _received.Reader.ReadAsync(timeout.Token);
    }
}
