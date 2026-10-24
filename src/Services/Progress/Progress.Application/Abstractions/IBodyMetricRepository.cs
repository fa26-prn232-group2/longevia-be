using Progress.Domain.Entities;

namespace Progress.Application.Abstractions;

public interface IBodyMetricRepository
{
    void Add(BodyMetricLog log);
    Task<BodyMetricLog?> GetLatestAsync(Guid userId, CancellationToken ct);
    Task<BodyMetricLog?> GetLatestBeforeDateAsync(Guid userId, DateTimeOffset cutoffUtc, CancellationToken ct);
    Task<BodyMetricLog?> GetEarliestAsync(Guid userId, CancellationToken ct);
    Task<(IReadOnlyList<BodyMetricLog> Items, int TotalCount)> GetHistoryAsync(
        Guid userId, DateOnly? fromDate, DateOnly? toDate, int pageNumber, int pageSize, CancellationToken ct);
    Task<IReadOnlyList<DateOnly>> GetActiveMetricDatesAsync(Guid userId, CancellationToken ct);
}
