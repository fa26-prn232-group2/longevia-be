using System.Data;
using Longevia.Shared.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Progress.Application.Abstractions;
using Progress.Domain.Entities;
using Progress.Infrastructure.Persistence;

namespace Progress.Infrastructure.Services;

public sealed class MealActivityConsumerService(
    ProgressDbContext db,
    ILogger<MealActivityConsumerService> logger) : IMealActivityConsumerService
{
    private const string MealSource = "Diet.Meal";

    public async Task<MealProcessingResult> ProcessMealLoggedAsync(MealLoggedEvent @event, CancellationToken ct)
    {
        if (@event.EventId == Guid.Empty || @event.UserId == Guid.Empty || @event.MealLogId == Guid.Empty)
        {
            throw new ArgumentException("MealLoggedEvent contains empty IDs.");
        }

        var sourceLogId = @event.MealLogId.ToString();

        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);

            // Khóa phạm vi MealLogId bằng transaction advisory lock
            await db.Database.ExecuteSqlRawAsync(
                "SELECT pg_advisory_xact_lock(hashtext({0}))", [@event.MealLogId.ToString()], ct);

            var existingEvent = await db.ProcessedMealEvents
                .FirstOrDefaultAsync(x => x.EventId == @event.EventId, ct);

            var existingContrib = await db.ActivityContributions
                .FirstOrDefaultAsync(x => x.Source == MealSource && x.SourceLogId == sourceLogId, ct);

            if (existingEvent != null || existingContrib != null)
            {
                var conflictUserId = (existingEvent != null && existingEvent.UserId != @event.UserId)
                    || (existingContrib != null && existingContrib.UserId != @event.UserId);

                var conflictDate = (existingEvent != null && existingEvent.BusinessDate != @event.BusinessDate)
                    || (existingContrib != null && existingContrib.Date != @event.BusinessDate);

                if (conflictUserId || conflictDate)
                {
                    logger.LogWarning(
                        "Conflicting duplicate MealLoggedEvent detected. MealLogId: {MealLogId}, ExpectedUser: {User1}, ReceivedUser: {User2}, ExpectedDate: {Date1}, ReceivedDate: {Date2}",
                        @event.MealLogId,
                        existingEvent?.UserId ?? existingContrib?.UserId,
                        @event.UserId,
                        existingEvent?.BusinessDate ?? existingContrib?.Date,
                        @event.BusinessDate);

                    await tx.RollbackAsync(ct);
                    return MealProcessingResult.ConflictingDuplicate;
                }

                if (existingEvent == null)
                {
                    db.ProcessedMealEvents.Add(ProcessedMealEvent.Create(
                        @event.EventId, @event.MealLogId, @event.UserId, @event.BusinessDate, DateTimeOffset.UtcNow));
                    await db.SaveChangesAsync(ct);
                    await tx.CommitAsync(ct);
                }

                return MealProcessingResult.AlreadyProcessed;
            }

            var contribution = ActivityContribution.Create(
                @event.UserId,
                MealSource,
                sourceLogId,
                @event.BusinessDate,
                @event.CreatedAt);

            var processedEvent = ProcessedMealEvent.Create(
                @event.EventId,
                @event.MealLogId,
                @event.UserId,
                @event.BusinessDate,
                DateTimeOffset.UtcNow);

            db.ActivityContributions.Add(contribution);
            db.ProcessedMealEvents.Add(processedEvent);

            try
            {
                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                return MealProcessingResult.Created;
            }
            catch (DbUpdateException ex) when (IsUniqueViolation(ex))
            {
                await tx.RollbackAsync(ct);
                return MealProcessingResult.AlreadyProcessed;
            }
        });
    }

    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException?.Message.Contains("23505") == true ||
        ex.Message.Contains("unique", StringComparison.OrdinalIgnoreCase);
}
