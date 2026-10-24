using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Longevia.Shared.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Progress.Application.Abstractions;
using Progress.Application.Features.Dashboard;
using Progress.Application.Features.Metrics;
using Progress.Domain.Entities;
using Progress.Infrastructure.Persistence;

namespace Progress.IntegrationTests.Messaging;

[Collection("Progress foundation")]
public sealed class MealActivityConsumerTests(ProgressFixture fixture)
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    [Fact]
    public async Task MealConsumer_ValidEvent_CreatesActivityContributionAndInbox()
    {
        var factory = fixture.Factory;
        using var scope = factory.Services.CreateScope();
        var consumerService = scope.ServiceProvider.GetRequiredService<IMealActivityConsumerService>();
        var db = scope.ServiceProvider.GetRequiredService<ProgressDbContext>();

        var userId = Guid.NewGuid();
        var mealLogId = Guid.NewGuid();
        var eventId = mealLogId; // v1: EventId == MealLogId
        var businessDate = new DateOnly(2026, 10, 24);
        var createdAt = DateTimeOffset.UtcNow.AddMinutes(-5);

        var @event = new MealLoggedEvent(
            eventId,
            userId,
            mealLogId,
            businessDate,
            createdAt,
            OccurredOn: DateTimeOffset.UtcNow);

        var result = await consumerService.ProcessMealLoggedAsync(@event, CancellationToken.None);
        Assert.Equal(MealProcessingResult.Created, result);

        // Verify in DB
        var contrib = await db.ActivityContributions
            .FirstOrDefaultAsync(x => x.Source == "Diet.Meal" && x.SourceLogId == mealLogId.ToString());
        Assert.NotNull(contrib);
        Assert.Equal(userId, contrib.UserId);
        Assert.Equal(businessDate, contrib.Date);

        var inbox = await db.ProcessedMealEvents.FirstOrDefaultAsync(x => x.EventId == eventId);
        Assert.NotNull(inbox);
        Assert.Equal(mealLogId, inbox.MealLogId);
        Assert.Equal(userId, inbox.UserId);
        Assert.Equal(businessDate, inbox.BusinessDate);
    }

    [Fact]
    public async Task MealConsumer_SameEventDeliveredTwice_IdempotentNoDuplicate()
    {
        var factory = fixture.Factory;
        using var scope = factory.Services.CreateScope();
        var consumerService = scope.ServiceProvider.GetRequiredService<IMealActivityConsumerService>();
        var db = scope.ServiceProvider.GetRequiredService<ProgressDbContext>();

        var userId = Guid.NewGuid();
        var mealLogId = Guid.NewGuid();
        var businessDate = new DateOnly(2026, 10, 23);

        var @event = new MealLoggedEvent(
            mealLogId, userId, mealLogId, businessDate, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

        // First delivery
        var res1 = await consumerService.ProcessMealLoggedAsync(@event, CancellationToken.None);
        Assert.Equal(MealProcessingResult.Created, res1);

        // Second delivery (redelivery / replay)
        var res2 = await consumerService.ProcessMealLoggedAsync(@event, CancellationToken.None);
        Assert.Equal(MealProcessingResult.AlreadyProcessed, res2);

        // Exact 1 record in DB
        var count = await db.ActivityContributions
            .CountAsync(x => x.Source == "Diet.Meal" && x.SourceLogId == mealLogId.ToString());
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task MealConsumer_ConcurrentDuplicateDelivery_ThreadSafe()
    {
        var factory = fixture.Factory;

        var userId = Guid.NewGuid();
        var mealLogId = Guid.NewGuid();
        var businessDate = new DateOnly(2026, 10, 22);

        var @event = new MealLoggedEvent(
            mealLogId, userId, mealLogId, businessDate, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

        // Bắn 10 consumer request đồng thời cho cùng 1 event
        var tasks = Enumerable.Range(0, 10).Select(async _ =>
        {
            using var scope = factory.Services.CreateScope();
            var consumerService = scope.ServiceProvider.GetRequiredService<IMealActivityConsumerService>();
            return await consumerService.ProcessMealLoggedAsync(@event, CancellationToken.None);
        }).ToList();

        var results = await Task.WhenAll(tasks);

        var createdCount = results.Count(r => r == MealProcessingResult.Created);
        var alreadyCount = results.Count(r => r == MealProcessingResult.AlreadyProcessed);

        Assert.Equal(1, createdCount);
        Assert.Equal(9, alreadyCount);

        using var verifyScope = factory.Services.CreateScope();
        var db = verifyScope.ServiceProvider.GetRequiredService<ProgressDbContext>();
        var count = await db.ActivityContributions
            .CountAsync(x => x.Source == "Diet.Meal" && x.SourceLogId == mealLogId.ToString());
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task MealConsumer_ConflictingDuplicate_RejectsAndDoesNotOverwriteOriginal()
    {
        var factory = fixture.Factory;
        using var scope = factory.Services.CreateScope();
        var consumerService = scope.ServiceProvider.GetRequiredService<IMealActivityConsumerService>();
        var db = scope.ServiceProvider.GetRequiredService<ProgressDbContext>();

        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();
        var mealLogId = Guid.NewGuid();
        var dateA = new DateOnly(2026, 10, 20);
        var dateB = new DateOnly(2026, 10, 21);

        var eventOriginal = new MealLoggedEvent(
            mealLogId, userA, mealLogId, dateA, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

        var eventConflictUser = new MealLoggedEvent(
            mealLogId, userB, mealLogId, dateA, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

        var eventConflictDate = new MealLoggedEvent(
            mealLogId, userA, mealLogId, dateB, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

        // 1. Process original
        var res1 = await consumerService.ProcessMealLoggedAsync(eventOriginal, CancellationToken.None);
        Assert.Equal(MealProcessingResult.Created, res1);

        // 2. Conflicting user
        var resConflictUser = await consumerService.ProcessMealLoggedAsync(eventConflictUser, CancellationToken.None);
        Assert.Equal(MealProcessingResult.ConflictingDuplicate, resConflictUser);

        // 3. Conflicting date
        var resConflictDate = await consumerService.ProcessMealLoggedAsync(eventConflictDate, CancellationToken.None);
        Assert.Equal(MealProcessingResult.ConflictingDuplicate, resConflictDate);

        // Dữ liệu gốc của userA không bị thay đổi
        var originalInDb = await db.ActivityContributions
            .FirstAsync(x => x.Source == "Diet.Meal" && x.SourceLogId == mealLogId.ToString());
        Assert.Equal(userA, originalInDb.UserId);
        Assert.Equal(dateA, originalInDb.Date);
    }

    [Fact]
    public async Task MealConsumer_StreakAggregation_CombinesMealsAndMetricsWithoutDoubleCounting()
    {
        var factory = fixture.Factory;
        using var scope = factory.Services.CreateScope();
        var consumerService = scope.ServiceProvider.GetRequiredService<IMealActivityConsumerService>();
        var db = scope.ServiceProvider.GetRequiredService<ProgressDbContext>();

        var userId = Guid.NewGuid();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var yesterday = today.AddDays(-1);

        // 1. Ghi nhận 2 meals vào hôm qua (2 bữa ăn cùng 1 ngày vẫn chỉ tính 1 active day)
        var meal1 = new MealLoggedEvent(Guid.NewGuid(), userId, Guid.NewGuid(), yesterday, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        var meal2 = new MealLoggedEvent(Guid.NewGuid(), userId, Guid.NewGuid(), yesterday, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        await consumerService.ProcessMealLoggedAsync(meal1, CancellationToken.None);
        await consumerService.ProcessMealLoggedAsync(meal2, CancellationToken.None);

        // 2. Ghi nhận 1 meal vào hôm nay VÀ 1 body metric vào hôm nay (cùng hôm nay không double count)
        var mealToday = new MealLoggedEvent(Guid.NewGuid(), userId, Guid.NewGuid(), today, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        await consumerService.ProcessMealLoggedAsync(mealToday, CancellationToken.None);

        var metricToday = BodyMetricLog.Create(userId, DateTimeOffset.UtcNow, 70.0m, 175.0m);
        db.BodyMetricLogs.Add(metricToday);
        await db.SaveChangesAsync();

        // 3. Gọi DashboardSummary
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", TestTokens.Create(fixture, "valid", userId));

        var response = await client.GetAsync("/dashboard/summary");
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);

        var summary = await response.Content.ReadFromJsonAsync<DashboardSummaryResponse>(JsonOpts);
        Assert.NotNull(summary);
        // Hôm qua active (2 bữa), hôm nay active (1 bữa + 1 metric) => streak = 2 ngày
        Assert.Equal(2, summary.CurrentStreakDays);
        Assert.True(summary.BestStreakDays >= 2);
    }

    [Fact]
    public async Task MealConsumer_BackdatedEvent_UpdatesHistoricalActivityDate()
    {
        var factory = fixture.Factory;
        using var scope = factory.Services.CreateScope();
        var consumerService = scope.ServiceProvider.GetRequiredService<IMealActivityConsumerService>();
        var db = scope.ServiceProvider.GetRequiredService<ProgressDbContext>();

        var userId = Guid.NewGuid();
        var backdatedDate = new DateOnly(2026, 1, 15);
        var mealId = Guid.NewGuid();

        // Event tạo lúc này nhưng mang BusinessDate 9 tháng trước
        var backdatedEvent = new MealLoggedEvent(
            mealId, userId, mealId, backdatedDate, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

        var result = await consumerService.ProcessMealLoggedAsync(backdatedEvent, CancellationToken.None);
        Assert.Equal(MealProcessingResult.Created, result);

        var record = await db.ActivityContributions.FirstAsync(x => x.SourceLogId == mealId.ToString());
        Assert.Equal(backdatedDate, record.Date);
    }
}
