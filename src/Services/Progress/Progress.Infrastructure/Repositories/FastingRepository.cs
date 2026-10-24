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

    public async Task<IReadOnlyList<FastingSession>> GetCompletedSessionsInWeekAsync(
        Guid userId, DateTimeOffset weekStartUtc, DateTimeOffset weekEndUtc, CancellationToken ct)
    {
        return await db.FastingSessions
            .Where(x => x.UserId == userId && x.EndedAt != null && x.EndedAt >= weekStartUtc && x.EndedAt <= weekEndUtc)
            .ToListAsync(ct);
    }
}
