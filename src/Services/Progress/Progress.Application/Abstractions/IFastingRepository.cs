using Progress.Domain.Entities;

namespace Progress.Application.Abstractions;

public interface IFastingRepository
{
    void Add(FastingSession session);
    Task<bool> HasActiveSessionAsync(Guid userId, CancellationToken ct);
    Task<FastingSession?> GetActiveSessionAsync(Guid userId, CancellationToken ct);
    Task<FastingSession?> GetByIdAsync(Guid id, Guid userId, CancellationToken ct);
    Task<FastingSession?> EndSessionWithLockAsync(Guid sessionId, Guid userId, DateTimeOffset endedAt, CancellationToken ct);
    Task<IReadOnlyList<FastingSession>> GetCompletedSessionsInWeekAsync(Guid userId, DateTimeOffset weekStartUtc, DateTimeOffset weekEndUtc, CancellationToken ct);
}
