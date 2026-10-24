using Diet.Domain.Entities;

namespace Diet.Application.Abstractions;

public interface ITargetRepository
{
    Task<DailyDietTarget?> GetAsync(Guid userId, CancellationToken ct);
    Task<DailyDietTarget> UpsertAsync(DailyDietTarget target, CancellationToken ct);
}
