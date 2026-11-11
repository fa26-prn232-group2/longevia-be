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

    public async Task<IReadOnlyList<DailyWaterTotal>> GetDailyTotalsAsync(
        Guid userId, DateOnly from, DateOnly to, CancellationToken ct)
    {
        return await db.WaterLogs.AsNoTracking()
            .Where(x => x.UserId == userId && x.Date >= from && x.Date <= to)
            .GroupBy(x => x.Date)
            .Select(group => new DailyWaterTotal(group.Key, group.Sum(x => x.AmountMl)))
            .ToListAsync(ct);
    }

    public Task<WaterLog?> GetLatestAsync(Guid userId, DateOnly date, CancellationToken ct) =>
        db.WaterLogs.AsNoTracking()
            .Where(x => x.UserId == userId && x.Date == date)
            .OrderByDescending(x => x.LoggedAt)
            .ThenByDescending(x => x.Id)
            .FirstOrDefaultAsync(ct);

    public async Task<bool> DeleteAsync(Guid id, Guid userId, CancellationToken ct)
    {
        var log = await db.WaterLogs.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, ct);
        if (log is null) return false;
        db.WaterLogs.Remove(log);
        return true;
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

    public async Task<SleepLog> LogWithConcurrencyCheckAsync(
        Guid userId, DateTimeOffset sleptAt, DateTimeOffset wokeAt, string quality, CancellationToken ct)
    {
        // Sử dụng ExecutionStrategy và Transaction để khóa cạnh tranh ghi overlap của cùng 1 user
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, ct);

            // Khóa phạm vi user thông qua pg_advisory_xact_lock theo hash của userId để ngăn 2 request đồng thời
            var lockKey = userId.ToString();
            await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(hashtext({0}))", [lockKey], ct);

            var overlap = await db.SleepLogs
                .AnyAsync(x => x.UserId == userId && x.SleptAt < wokeAt && x.WokeAt > sleptAt, ct);

            if (overlap)
            {
                throw Application.Common.ProgressErrors.SleepOverlap();
            }

            var log = SleepLog.Create(userId, sleptAt, wokeAt, quality);
            db.SleepLogs.Add(log);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            return log;
        });
    }

    public async Task<SleepLog?> UpdateWithConcurrencyCheckAsync(Guid userId, Guid id,
        DateTimeOffset sleptAt, DateTimeOffset wokeAt, string quality, CancellationToken ct)
    {
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, ct);
            var lockKey = userId.ToString();
            await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(hashtext({0}))", [lockKey], ct);

            var log = await db.SleepLogs.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, ct);
            if (log is null)
            {
                await tx.RollbackAsync(ct);
                return null;
            }

            var overlap = await db.SleepLogs
                .AnyAsync(x => x.UserId == userId && x.Id != id && x.SleptAt < wokeAt && x.WokeAt > sleptAt, ct);
            if (overlap)
            {
                throw Application.Common.ProgressErrors.SleepOverlap();
            }

            log.Update(sleptAt, wokeAt, quality);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return log;
        });
    }

    public async Task<IReadOnlyList<SleepLog>> GetSleepLogsInDateRangeAsync(
        Guid userId, DateOnly fromDate, DateOnly toDate, CancellationToken ct)
    {
        return await db.SleepLogs
            .Where(x => x.UserId == userId && x.Date >= fromDate && x.Date <= toDate)
            .OrderByDescending(x => x.WokeAt)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<SleepLog>> GetPageAsync(
        Guid userId, int skip, int take, CancellationToken ct)
    {
        return await db.SleepLogs.AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.WokeAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);
    }

    public Task<int> CountAsync(Guid userId, CancellationToken ct) =>
        db.SleepLogs.CountAsync(x => x.UserId == userId, ct);

    public async Task<bool> DeleteAsync(Guid id, Guid userId, CancellationToken ct)
    {
        var log = await db.SleepLogs.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, ct);
        if (log is null) return false;
        db.SleepLogs.Remove(log);
        return true;
    }
}
