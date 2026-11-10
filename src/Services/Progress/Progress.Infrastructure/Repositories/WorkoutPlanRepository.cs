using Microsoft.EntityFrameworkCore;
using Progress.Application.Abstractions;
using Progress.Domain.Entities;
using Progress.Infrastructure.Persistence;

namespace Progress.Infrastructure.Repositories;

public sealed class WorkoutPlanRepository(ProgressDbContext db) : IWorkoutPlanRepository
{
    public async Task<WorkoutPlan?> GetAsync(Guid userId, DateOnly weekStartDate, CancellationToken ct) =>
        await db.WorkoutPlans.AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserId == userId && x.WeekStartDate == weekStartDate, ct);

    public async Task<WorkoutPlan> UpsertAsync(WorkoutPlan plan, CancellationToken ct)
    {
        var existing = await db.WorkoutPlans
            .FirstOrDefaultAsync(x => x.UserId == plan.UserId && x.WeekStartDate == plan.WeekStartDate, ct);
        if (existing is null)
        {
            db.WorkoutPlans.Add(plan);
            return plan;
        }
        existing.Replace(plan.Days, plan.SessionsPerWeek, plan.MinutesPerSession, plan.Goal,
            plan.PlanJson, plan.GeneratedAt);
        return existing;
    }
}
