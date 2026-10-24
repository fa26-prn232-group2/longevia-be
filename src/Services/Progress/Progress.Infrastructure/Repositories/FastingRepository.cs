using Microsoft.EntityFrameworkCore;
using Progress.Application.Abstractions;
using Progress.Domain.Entities;
using Progress.Infrastructure.Persistence;

namespace Progress.Infrastructure.Repositories;

public sealed class FastingRepository(ProgressDbContext db) : IFastingRepository
{
    public void Add(FastingSession session)
    {
        db.FastingSessions.Add(session);
    }

    public async Task<bool> HasActiveSessionAsync(Guid userId, CancellationToken ct)
    {
        return await db.FastingSessions
            .AnyAsync(x => x.UserId == userId && x.EndedAt == null, ct);
    }

    public async Task<FastingSession?> GetActiveSessionAsync(Guid userId, CancellationToken ct)
    {
        return await db.FastingSessions
            .Where(x => x.UserId == userId && x.EndedAt == null)
            .OrderByDescending(x => x.StartedAt)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<FastingSession?> GetByIdAsync(Guid id, Guid userId, CancellationToken ct)
    {
        return await db.FastingSessions
            .FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, ct);
    }

    public async Task<FastingSession?> EndSessionWithLockAsync(
        Guid sessionId, Guid userId, DateTimeOffset endedAt, CancellationToken ct)
    {
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, ct);

            // Khóa mức transaction-scoped advisory lock theo sessionId để tuần tự hóa mọi request end đồng thời
            var lockKey = sessionId.ToString();
            await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(hashtext({0}))", [lockKey], ct);

            var session = await db.FastingSessions
                .FirstOrDefaultAsync(x => x.Id == sessionId && x.UserId == userId, ct);

            if (session == null)
            {
                await tx.RollbackAsync(ct);
                throw Application.Common.ProgressErrors.FastingSessionNotFound();
            }

            if (session.Status == Domain.Constants.FastingStatus.Completed)
            {
                // Q03 Idempotency / Retry semantics:
                // Cùng sessionId và cùng endedAt (chênh lệch <= 1 giây) -> replay 200 OK
                // Nếu payload endedAt khác (> 1 giây) -> 409 Conflict
                if (session.EndedAt.HasValue && Math.Abs((session.EndedAt.Value - endedAt).TotalSeconds) > 1.0)
                {
                    await tx.RollbackAsync(ct);
                    throw Application.Common.ProgressErrors.FastingEndConflict();
                }

                await tx.CommitAsync(ct);
                return session;
            }

            if (endedAt < session.StartedAt)
            {
                await tx.RollbackAsync(ct);
                throw Application.Common.ProgressErrors.FastingEndedBeforeStarted();
            }

            session.End(endedAt);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            return session;
        });
    }

    public async Task<IReadOnlyList<FastingSession>> GetCompletedSessionsInWeekAsync(
        Guid userId, DateTimeOffset weekStartUtc, DateTimeOffset weekEndUtc, CancellationToken ct)
    {
        return await db.FastingSessions
            .Where(x => x.UserId == userId && x.EndedAt != null && x.EndedAt >= weekStartUtc && x.EndedAt < weekEndUtc)
            .ToListAsync(ct);
    }
}
