using Diet.Domain.Entities;

namespace Diet.Application.Abstractions;

public interface IMealPlanRepository
{
    Task<MealPlan?> GetAsync(Guid userId, DateOnly weekStartDate, CancellationToken ct);
    Task<MealPlan> UpsertAsync(MealPlan plan, CancellationToken ct);
}
