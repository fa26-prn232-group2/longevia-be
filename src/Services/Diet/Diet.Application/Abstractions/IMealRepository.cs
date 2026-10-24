using Diet.Domain.Entities;

namespace Diet.Application.Abstractions;

public interface IMealRepository
{
    void Add(MealLog meal);
    Task<IReadOnlyList<MealLog>> GetDailyAsync(Guid userId, DateOnly date, CancellationToken ct);
}
