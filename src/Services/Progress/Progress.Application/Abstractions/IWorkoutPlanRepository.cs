using Progress.Domain.Entities;

namespace Progress.Application.Abstractions;

public interface IWorkoutPlanRepository
{
    Task<WorkoutPlan?> GetAsync(Guid userId, DateOnly weekStartDate, CancellationToken ct);
    Task<WorkoutPlan> UpsertAsync(WorkoutPlan plan, CancellationToken ct);
}
