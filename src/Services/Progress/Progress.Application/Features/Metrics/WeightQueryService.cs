using Progress.Application.Abstractions;

namespace Progress.Application.Features.Metrics;

public sealed record LatestWeightResult(
    bool Found,
    int? WeightKgX100 = null,
    DateTimeOffset? MeasuredAt = null,
    Guid? SourceMetricId = null);

public interface IWeightQueryService
{
    Task<LatestWeightResult> GetLatestWeightAsync(Guid userId, DateTimeOffset? asOfUtc, CancellationToken ct);
}

public sealed class WeightQueryService(
    IBodyMetricRepository metricRepo,
    TimeProvider timeProvider) : IWeightQueryService
{
    public async Task<LatestWeightResult> GetLatestWeightAsync(Guid userId, DateTimeOffset? asOfUtc, CancellationToken ct)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("UserId cannot be empty.", nameof(userId));
        }

        var now = timeProvider.GetUtcNow();
        var effectiveAsOf = asOfUtc.HasValue && asOfUtc.Value > now
            ? now
            : (asOfUtc ?? now);

        var metric = await metricRepo.GetLatestBeforeDateAsync(userId, effectiveAsOf, ct);
        if (metric == null)
        {
            return new LatestWeightResult(false);
        }

        var weightX100 = checked((int)Math.Round(metric.WeightKg * 100m, MidpointRounding.AwayFromZero));

        return new LatestWeightResult(
            Found: true,
            WeightKgX100: weightX100,
            MeasuredAt: metric.LoggedAt,
            SourceMetricId: metric.Id);
    }
}
