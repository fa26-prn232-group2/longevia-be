using Diet.Application.Abstractions;
using Diet.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Diet.Infrastructure.Persistence.Repositories;

public sealed class MealPlanRepository(DietDbContext db) : IMealPlanRepository
{
    public async Task<MealPlan?> GetAsync(Guid userId, DateOnly weekStartDate, CancellationToken ct) =>
        await db.MealPlans.AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserId == userId && x.WeekStartDate == weekStartDate, ct);

    public async Task<MealPlan> UpsertAsync(MealPlan plan, CancellationToken ct)
    {
        var existing = await db.MealPlans
            .FirstOrDefaultAsync(x => x.UserId == plan.UserId && x.WeekStartDate == plan.WeekStartDate, ct);
        if (existing is null)
        {
            db.MealPlans.Add(plan);
            return plan;
        }

        existing.Replace(plan.PlanJson, plan.GeneratedAt);
        return existing;
    }
}
