using Longevia.Diet.Contracts.V1;
using Longevia.Shared.Events;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Progress.Application.Abstractions;
using Progress.Infrastructure.Persistence;

namespace Progress.Infrastructure.Messaging;

/// <summary>
/// Chạy mỗi thứ Hai 00:00 UTC (hoặc ngay khi khởi động nếu bật RUN_ON_START),
/// tổng hợp tuần trước và publish <see cref="WeeklyDigestReadyEvent"/> cho user
/// đã opt-in. Không gửi email trực tiếp; Worker sở hữu việc gửi.
/// </summary>
public sealed class WeeklyDigestScheduler(IServiceScopeFactory scopeFactory, IConfiguration config,
    ILogger<WeeklyDigestScheduler> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!bool.TryParse(config["DIGEST:ENABLED"], out var enabled) || !enabled)
        {
            logger.LogInformation("Weekly digest scheduler disabled.");
            return;
        }

        if (!bool.TryParse(config["DIGEST:RUN_ON_START"], out var runOnStart) || !runOnStart)
        {
            await DelayUntilNextMondayAsync(stoppingToken);
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogError(exception, "Weekly digest run failed.");
            }

            await Task.Delay(TimeSpan.FromDays(7), stoppingToken);
        }
    }

    private async Task RunOnceAsync(CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ProgressDbContext>();
        var contacts = scope.ServiceProvider.GetRequiredService<IUserContactProvider>();
        var adherence = scope.ServiceProvider.GetRequiredService<INutritionAdherenceProvider>();
        var publish = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();

        var today = Progress.Domain.UtcCalendar.Day(DateTimeOffset.UtcNow);
        var from = today.AddDays(-6);
        var userIds = await db.ActivityContributions
            .Where(x => x.Date >= from && x.Date <= today)
            .Select(x => x.UserId).Distinct().ToListAsync(ct);

        foreach (var userId in userIds)
        {
            UserContactResult contact;
            try
            {
                contact = await contacts.GetAsync(userId, ct);
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Digest skipped: Identity contact unavailable.");
                continue;
            }
            if (!contact.ReceiveWeeklyDigest) continue;

            var dates = await db.ActivityContributions
                .Where(x => x.UserId == userId && x.Date >= from && x.Date <= today)
                .Select(x => x.Date).Distinct().ToListAsync(ct);
            var currentStreak = CalculateCurrentStreak(dates, today);
            NutritionAdherenceResponse adherenceResponse;
            try
            {
                adherenceResponse = await adherence.GetAsync(userId, from, today, ct);
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Digest skipped: Diet adherence unavailable.");
                continue;
            }

            await publish.Publish(new WeeklyDigestReadyEvent(Guid.NewGuid(), userId, contact.Email,
                contact.FullName, from.ToString("yyyy-MM-dd"), today.ToString("yyyy-MM-dd"),
                (int)Math.Round((double)adherenceResponse.AdherenceRate * 2000), 0, currentStreak,
                currentStreak, dates.Count, DateTimeOffset.UtcNow), ct);
        }

        logger.LogInformation("Weekly digest events published: {Count}", userIds.Count);
    }

    private static int CalculateCurrentStreak(IReadOnlyCollection<DateOnly> dates, DateOnly today)
    {
        var set = dates.ToHashSet();
        var cursor = set.Contains(today) ? today : today.AddDays(-1);
        var streak = 0;
        while (set.Contains(cursor))
        {
            streak++;
            cursor = cursor.AddDays(-1);
        }
        return streak;
    }

    private static async Task DelayUntilNextMondayAsync(CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var today = Progress.Domain.UtcCalendar.Day(now);
        var days = ((int)DayOfWeek.Monday - (int)today.DayOfWeek + 7) % 7;
        var nextMonday = today.AddDays(days);
        var next = Progress.Domain.UtcCalendar.StartOfDayUtc(nextMonday);
        if (next <= now)
        {
            next = Progress.Domain.UtcCalendar.StartOfDayUtc(nextMonday.AddDays(7));
        }

        var delay = next - now;
        if (delay > TimeSpan.Zero) await Task.Delay(delay, ct);
    }
}
