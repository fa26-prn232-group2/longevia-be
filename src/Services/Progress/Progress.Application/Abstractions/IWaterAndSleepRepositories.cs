using Progress.Domain.Entities;

namespace Progress.Application.Abstractions;

public interface IWaterRepository
{
    void Add(WaterLog log);
    Task<int> GetTotalConsumedMlAsync(Guid userId, DateOnly date, CancellationToken ct);
    Task<IReadOnlyList<DailyWaterTotal>> GetDailyTotalsAsync(Guid userId, DateOnly from, DateOnly to,
        CancellationToken ct);
    Task<WaterLog?> GetLatestAsync(Guid userId, DateOnly date, CancellationToken ct);
    Task<bool> DeleteAsync(Guid id, Guid userId, CancellationToken ct);
}

/// <summary>Tổng lượng nước của một ngày nghiệp vụ.</summary>
public sealed record DailyWaterTotal(DateOnly Date, int AmountMl);

public interface ISleepRepository
{
    void Add(SleepLog log);
    Task<bool> HasOverlapAsync(Guid userId, DateTimeOffset sleptAt, DateTimeOffset wokeAt, CancellationToken ct);
    Task<SleepLog> LogWithConcurrencyCheckAsync(Guid userId, DateTimeOffset sleptAt, DateTimeOffset wokeAt, string quality, CancellationToken ct);
    Task<SleepLog?> UpdateWithConcurrencyCheckAsync(Guid userId, Guid id, DateTimeOffset sleptAt,
        DateTimeOffset wokeAt, string quality, CancellationToken ct);
    Task<IReadOnlyList<SleepLog>> GetSleepLogsInDateRangeAsync(Guid userId, DateOnly fromDate, DateOnly toDate, CancellationToken ct);
    Task<IReadOnlyList<SleepLog>> GetPageAsync(Guid userId, int skip, int take, CancellationToken ct);
    Task<int> CountAsync(Guid userId, CancellationToken ct);
    Task<bool> DeleteAsync(Guid id, Guid userId, CancellationToken ct);
}
