using Microsoft.EntityFrameworkCore;
using Progress.Application.Abstractions;
using Progress.Domain.Entities;
using Progress.Infrastructure.Persistence;

namespace Progress.Infrastructure.Repositories;

public sealed class BodyMetricRepository(ProgressDbContext db) : IBodyMetricRepository
{
    public void Add(BodyMetricLog log)
    {
        db.BodyMetricLogs.Add(log);
    }

    public async Task<BodyMetricLog?> GetLatestAsync(Guid userId, CancellationToken ct)
    {
        return await db.BodyMetricLogs
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.LoggedAt)
            .ThenByDescending(x => x.Id)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<BodyMetricLog?> GetLatestBeforeDateAsync(Guid userId, DateTimeOffset cutoffUtc, CancellationToken ct)
    {
        return await db.BodyMetricLogs
            .Where(x => x.UserId == userId && x.LoggedAt <= cutoffUtc)
            .OrderByDescending(x => x.LoggedAt)
            .ThenByDescending(x => x.Id)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<BodyMetricLog?> GetEarliestAsync(Guid userId, CancellationToken ct)
    {
        return await db.BodyMetricLogs
            .Where(x => x.UserId == userId)
            .OrderBy(x => x.LoggedAt)
            .ThenBy(x => x.Id)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<(IReadOnlyList<BodyMetricLog> Items, int TotalCount)> GetHistoryAsync(
        Guid userId, DateOnly? fromDate, DateOnly? toDate, int pageNumber, int pageSize, CancellationToken ct)
    {
        var query = db.BodyMetricLogs.Where(x => x.UserId == userId);

        if (fromDate.HasValue)
        {
            var fromUtc = fromDate.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            query = query.Where(x => x.LoggedAt >= fromUtc);
        }

        if (toDate.HasValue)
        {
            var toUtc = toDate.Value.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);
            query = query.Where(x => x.LoggedAt <= toUtc);
        }

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(x => x.LoggedAt)
            .ThenByDescending(x => x.Id)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public async Task<IReadOnlyList<DateOnly>> GetActiveMetricDatesAsync(Guid userId, CancellationToken ct)
    {
        var dates = await db.BodyMetricLogs
            .Where(x => x.UserId == userId)
            .Select(x => x.LoggedAt)
            .ToListAsync(ct);

        return dates.Select(d => DateOnly.FromDateTime(d.UtcDateTime)).Distinct().ToList();
    }
}
