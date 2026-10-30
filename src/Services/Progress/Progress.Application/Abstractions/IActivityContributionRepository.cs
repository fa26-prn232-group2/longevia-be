using Progress.Domain.Entities;

namespace Progress.Application.Abstractions;

public interface IActivityContributionRepository
{
    void Add(ActivityContribution contribution);
    Task<bool> ExistsAsync(string source, string sourceLogId, CancellationToken ct);
    Task<IReadOnlyList<DateOnly>> GetMealActiveDatesAsync(Guid userId, CancellationToken ct);
}
