using System.Net.Http.Headers;
using System.Net.Http.Json;
using Longevia.Shared.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Progress.Application.Abstractions;
using Progress.Application.Features.Dashboard;
using Progress.Application.Features.Water;
using Progress.Domain;
using Progress.Domain.Entities;
using Progress.Infrastructure.Persistence;
using Progress.Infrastructure.Repositories;

namespace Progress.IntegrationTests;

[Collection("Progress foundation")]
public sealed class UtcPolicyTests(ProgressFixture fixture)
{
    private static readonly System.Text.Json.JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    [Fact]
    public async Task UtcMidnight_WaterTodayChanges_WithoutLeakingPreviousDay()
    {
        var clock = new MutableClock { Now = DateTimeOffset.Parse("2026-10-24T23:59:59Z") };
        await using var factory = new ProgressApiFactory(fixture, time: clock);
        using var client = factory.CreateClient();
        var user = Guid.NewGuid();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestTokens.Create(fixture, "valid", user));
        var first = await client.PostAsJsonAsync("/water/log", new { amountMl = 350 });
        first.EnsureSuccessStatusCode();
        Assert.Equal(new DateOnly(2026, 10, 24), (await first.Content.ReadFromJsonAsync<WaterSummaryResponse>(Json))!.Date);
        clock.Now = DateTimeOffset.Parse("2026-10-25T00:00:00Z");
        var today = await client.GetFromJsonAsync<WaterSummaryResponse>("/water/today", Json);
        Assert.Equal(new DateOnly(2026, 10, 25), today!.Date);
        Assert.Equal(0, today.ConsumedMl);
        var second = await client.PostAsJsonAsync("/water/log", new { amountMl = 650 });
        second.EnsureSuccessStatusCode();
        Assert.Equal(650, (await second.Content.ReadFromJsonAsync<WaterSummaryResponse>(Json))!.ConsumedMl);
        // Vietnam 00:30 is still the previous UTC day, by deliberate MVP policy.
        Assert.Equal(new DateOnly(2026, 10, 24), UtcCalendar.Day(DateTimeOffset.Parse("2026-10-25T00:30:00+07:00")));
    }

    [Fact]
    public async Task UtcPolicy_SleepMetricHistoryMealReplayAndDashboard_PreserveCanonicalDays()
    {
        var before = DateTimeOffset.Parse("2026-10-24T23:59:59Z");
        var midnight = DateTimeOffset.Parse("2026-10-25T00:00:00Z");
        var user = Guid.NewGuid();
        var sleep = SleepLog.Create(user, before.AddHours(-8), before, "Good");
        var overnight = SleepLog.Create(user, midnight.AddHours(-8), midnight, "Good");
        Assert.Equal(new DateOnly(2026, 10, 24), sleep.Date);
        Assert.Equal(new DateOnly(2026, 10, 25), overnight.Date);
        Assert.Equal(8, overnight.DurationHours);
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ProgressDbContext>();
        db.BodyMetricLogs.AddRange(BodyMetricLog.Create(user, before, 70, 175), BodyMetricLog.Create(user, midnight, 69, 175));
        await db.SaveChangesAsync();
        var repository = new BodyMetricRepository(db);
        var history = await repository.GetHistoryAsync(user, new DateOnly(2026, 10, 24), new DateOnly(2026, 10, 24), 1, 20, default);
        Assert.Single(history.Items);
        Assert.Equal(before, history.Items[0].LoggedAt);
        Assert.Equal(2, (await repository.GetActiveMetricDatesAsync(user, default)).Count);
        var id = Guid.NewGuid();
        var meal = new MealLoggedEvent(id, user, id, new DateOnly(2026, 10, 24), midnight, midnight.AddDays(20));
        var consumer = scope.ServiceProvider.GetRequiredService<IMealActivityConsumerService>();
        await consumer.ProcessMealLoggedAsync(meal, default);
        Assert.Equal(MealProcessingResult.AlreadyProcessed, await consumer.ProcessMealLoggedAsync(meal with { OccurredOn = midnight.AddDays(40) }, default));
        Assert.Equal(meal.BusinessDate, (await db.ActivityContributions.SingleAsync(x => x.SourceLogId == id.ToString())).Date);
        using var client = fixture.Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestTokens.Create(fixture, "valid", user));
        var summary = await client.GetFromJsonAsync<DashboardSummaryResponse>("/dashboard/summary", Json);
        Assert.Equal(2, summary!.CurrentStreakDays);
        Assert.Equal(2, summary.BestStreakDays);
        Assert.Equal(DateTimeOffset.Parse("2026-10-19T00:00:00Z"), UtcCalendar.StartOfWeek(before));
        Assert.Equal(DateTimeOffset.Parse("2026-10-26T00:00:00Z"), UtcCalendar.StartOfWeek(DateTimeOffset.Parse("2026-10-26T07:00:00+07:00")));
    }

    private sealed class MutableClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; }
        public override DateTimeOffset GetUtcNow() => Now;
    }

    [Fact]
    public async Task FastingWeek_MondayUtc_IncludesStartAndExcludesNextMonday()
    {
        var monday = DateTimeOffset.Parse("2026-10-26T00:00:00Z");
        var user = Guid.NewGuid();
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ProgressDbContext>();
        foreach (var end in new[] { monday.AddSeconds(-1), monday, monday.AddDays(7) })
        {
            var session = FastingSession.Start(user, "16:8", 16, end.AddHours(-16));
            session.End(end);
            db.FastingSessions.Add(session);
        }
        await db.SaveChangesAsync();
        var records = await new FastingRepository(db).GetCompletedSessionsInWeekAsync(user, monday, monday.AddDays(7), default);
        Assert.Single(records);
        Assert.Equal(monday, records[0].EndedAt);
        await using var factory = new ProgressApiFactory(fixture, time: new MutableClock { Now = monday });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestTokens.Create(fixture, "valid", user));
        var summary = await client.GetFromJsonAsync<DashboardSummaryResponse>("/dashboard/summary", Json);
        Assert.Equal(1, summary!.FastingThisWeekCompleted);
    }
}
