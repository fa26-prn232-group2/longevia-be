using Microsoft.EntityFrameworkCore;
using Progress.Application.Abstractions;
using Progress.Domain.Entities;
using Progress.Infrastructure.Persistence;

namespace Progress.Infrastructure.Repositories;

public sealed class WaterRepository(ProgressDbContext db) : IWaterRepository
{
    public void Add(WaterLog log)
    {
        db.WaterLogs.Add(log);
    }

    public async Task<int> GetTotalConsumedMlAsync(Guid userId, DateOnly date, CancellationToken ct)
    {
        return await db.WaterLogs
            .Where(x => x.UserId == userId && x.Date == date)
            .SumAsync(x => x.AmountMl, ct);
    }
}

public sealed class SleepRepository(ProgressDbContext db) : ISleepRepository
{
    public void Add(SleepLog log)
    {
        db.SleepLogs.Add(log);
    }

    public async Task<bool> HasOverlapAsync(Guid userId, DateTimeOffset sleptAt, DateTimeOffset wokeAt, CancellationToken ct)
    {
        // Hai khoảng thời gian [S1, W1] và [S2, W2] overlap khi: S1 < W2 && W1 > S2
        return await db.SleepLogs
            .AnyAsync(x => x.UserId == userId && x.SleptAt < wokeAt && x.WokeAt > sleptAt, ct);
    }

    public async Task<IReadOnlyList<SleepLog>> GetSleepLogsInDateRangeAsync(
        Guid userId, DateOnly fromDate, DateOnly toDate, CancellationToken ct)
    {
        return await db.SleepLogs
            .Where(x => x.UserId == userId && x.Date >= fromDate && x.Date <= toDate)
            .OrderByDescending(x => x.WokeAt)
            .ToListAsync(ct);
    }
}
