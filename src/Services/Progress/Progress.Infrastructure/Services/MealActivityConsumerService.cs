using System.Data;
using Longevia.Shared.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using Progress.Application.Abstractions;
using Progress.Domain.Entities;
using Progress.Infrastructure.Persistence;

namespace Progress.Infrastructure.Services;

public sealed class MealActivityConsumerService(
    ProgressDbContext db,
    ILogger<MealActivityConsumerService> logger,
    TimeProvider time) : IMealActivityConsumerService
{
    private const string MealSource = "Diet.Meal";

    public async Task<MealProcessingResult> ProcessMealLoggedAsync(MealLoggedEvent message, CancellationToken ct)
    {
        if (message.EventId == Guid.Empty || message.UserId == Guid.Empty || message.EventId != message.MealLogId)
            throw new ArgumentException("Canonical Diet v1 requires nonempty IDs and EventId == MealLogId.");

        try
        {
            return await PersistAsync(message, ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: "PK_processed_meal_events" or "IX_activity_contributions_Source_SourceLogId"
        })
        {
            // Only known dedup keys are candidates. Re-read under the same lock and
            // compare metadata; unrelated uniqueness errors must reach broker retry/error.
            db.ChangeTracker.Clear();
            return await PersistAsync(message, ct);
        }
    }

    private async Task<MealProcessingResult> PersistAsync(MealLoggedEvent message, CancellationToken ct)
    {
        var sourceId = message.MealLogId.ToString();
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(hashtext({0}))", [sourceId], ct);
        var inbox = await db.ProcessedMealEvents.AsNoTracking()
            .Where(x => x.EventId == message.EventId || x.MealLogId == message.MealLogId).ToListAsync(ct);
        var contribution = await db.ActivityContributions.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Source == MealSource && x.SourceLogId == sourceId, ct);
        if (inbox.Count > 1 || inbox.Any(x => x.EventId != message.EventId || x.MealLogId != message.MealLogId
            || x.UserId != message.UserId || x.BusinessDate != message.BusinessDate)
            || (contribution is not null && (contribution.UserId != message.UserId
                || contribution.Date != message.BusinessDate
                || contribution.CreatedAt.UtcTicks / 10 != message.CreatedAt.UtcTicks / 10)))
        {
            logger.LogWarning("Rejected conflicting canonical meal projection; no persisted data changed.");
            return MealProcessingResult.ConflictingDuplicate;
        }
        if (contribution is not null && inbox.Count == 1)
            return MealProcessingResult.AlreadyProcessed;

        // A valid replay repairs a partial pair atomically, never acknowledges a missing effect.
        if (contribution is null)
            db.ActivityContributions.Add(ActivityContribution.Create(message.UserId, MealSource,
                sourceId, message.BusinessDate, message.CreatedAt));
        if (inbox.Count == 0)
            db.ProcessedMealEvents.Add(ProcessedMealEvent.Create(message.EventId, message.MealLogId,
                message.UserId, message.BusinessDate, time.GetUtcNow()));
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return contribution is null ? MealProcessingResult.Created : MealProcessingResult.AlreadyProcessed;
    }
}
