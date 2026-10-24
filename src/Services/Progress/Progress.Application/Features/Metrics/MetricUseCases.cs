using Longevia.Shared.Auth;
using Progress.Application.Abstractions;
using Progress.Application.Common;
using Progress.Domain.Entities;

namespace Progress.Application.Features.Metrics;

public sealed class MetricUseCases(
    IBodyMetricRepository metricRepo,
    IBodyProfileProvider profileProvider,
    IUnitOfWork uow,
    ICurrentUser currentUser)
{
    public async Task<BodyMetricResponse> LogAsync(LogBodyMetricRequest request, CancellationToken ct)
    {
        var userId = currentUser.RequiredId();

        // Lấy chiều cao từ Identity
        var heightCm = await profileProvider.GetUserHeightCmAsync(userId, ct);
        if (!heightCm.HasValue || heightCm.Value <= 0)
        {
            throw ProgressErrors.HeightUnavailable();
        }

        var log = BodyMetricLog.Create(
            userId,
            request.LoggedAt,
            request.WeightKg,
            heightCm.Value,
            request.BodyFatPercent,
            request.WaistCircumferenceCm,
            request.SystolicBloodPressure,
            request.DiastolicBloodPressure,
            request.RestingHeartRate);

        metricRepo.Add(log);
        await uow.SaveChangesAsync(ct);

        return ToResponse(log);
    }

    public async Task<MetricsHistoryResponse> GetHistoryAsync(
        DateOnly? fromDate, DateOnly? toDate, int pageNumber, int pageSize, CancellationToken ct)
    {
        var userId = currentUser.RequiredId();

        // Normalize pagination
        var normalizedPage = Math.Max(1, pageNumber);
        var normalizedSize = Math.Clamp(pageSize, 1, 100);

        var (items, totalCount) = await metricRepo.GetHistoryAsync(
            userId, fromDate, toDate, normalizedPage, normalizedSize, ct);

        var totalPages = (int)Math.Ceiling((double)totalCount / normalizedSize);

        var mapped = items.Select(ToResponse).ToList();

        return new MetricsHistoryResponse(
            mapped,
            normalizedPage,
            normalizedSize,
            totalCount,
            totalPages);
    }

    private static BodyMetricResponse ToResponse(BodyMetricLog log) =>
        new(
            log.Id,
            log.LoggedAt,
            log.WeightKg,
            log.Bmi,
            log.BmiCategory,
            log.BodyFatPercent,
            log.WaistCircumferenceCm,
            log.SystolicBloodPressure,
            log.DiastolicBloodPressure,
            log.RestingHeartRate);
}
