using Microsoft.EntityFrameworkCore;
using Progress.Application.Abstractions;
using Progress.Domain.Entities;
using Progress.Infrastructure.Persistence;

namespace Progress.Infrastructure.Repositories;

public sealed class ActivityContributionRepository(ProgressDbContext db) : IActivityContributionRepository
{
    public void Add(ActivityContribution contribution)
    {
        db.ActivityContributions.Add(contribution);
    }

    public async Task<bool> ExistsAsync(string source, string sourceLogId, CancellationToken ct)
    {
        return await db.ActivityContributions
            .AnyAsync(x => x.Source == source && x.SourceLogId == sourceLogId, ct);
    }

    public async Task<IReadOnlyList<DateOnly>> GetMealActiveDatesAsync(Guid userId, CancellationToken ct)
    {
        return await db.ActivityContributions
            .Where(x => x.UserId == userId && x.Source == "Diet.Meal")
            .Select(x => x.Date)
            .Distinct()
            .ToListAsync(ct);
    }
}
