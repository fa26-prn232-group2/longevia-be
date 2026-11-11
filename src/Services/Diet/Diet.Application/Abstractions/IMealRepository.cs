using Diet.Domain.Entities;

namespace Diet.Application.Abstractions;

public interface IMealRepository
{
    void Add(MealLog meal);
    Task<IReadOnlyList<MealLog>> GetDailyAsync(Guid userId, DateOnly date, CancellationToken ct);
    Task<IReadOnlyList<MealLog>> GetDailyTrackedAsync(Guid userId, DateOnly date, CancellationToken ct);
    Task<IReadOnlyList<MealLog>> GetRangeAsync(Guid userId, DateOnly from, DateOnly to, CancellationToken ct);
    void RemoveRange(IEnumerable<MealLog> meals);
}
