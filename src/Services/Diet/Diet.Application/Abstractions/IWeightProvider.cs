namespace Diet.Application.Abstractions;

public sealed record WeightSnapshot(decimal WeightKg, DateTimeOffset MeasuredAt,
    Guid SourceMetricId, DateTimeOffset RequestedAsOf);

public interface IWeightProvider
{
    Task<WeightSnapshot?> GetLatestAsync(Guid userId, DateTimeOffset asOf, CancellationToken ct);
}
