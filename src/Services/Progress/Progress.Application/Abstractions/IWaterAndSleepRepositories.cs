using Progress.Domain.Entities;

namespace Progress.Application.Abstractions;

public interface IWaterRepository
{
    void Add(WaterLog log);
    Task<int> GetTotalConsumedMlAsync(Guid userId, DateOnly date, CancellationToken ct);
}

public interface ISleepRepository
{
    void Add(SleepLog log);
    Task<bool> HasOverlapAsync(Guid userId, DateTimeOffset sleptAt, DateTimeOffset wokeAt, CancellationToken ct);
    Task<IReadOnlyList<SleepLog>> GetSleepLogsInDateRangeAsync(Guid userId, DateOnly fromDate, DateOnly toDate, CancellationToken ct);
}
