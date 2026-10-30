namespace Progress.Application.Features.Metrics;

public sealed record LogBodyMetricRequest(
    DateTimeOffset LoggedAt,
    decimal WeightKg,
    decimal? BodyFatPercent,
    decimal? WaistCircumferenceCm,
    int? SystolicBloodPressure,
    int? DiastolicBloodPressure,
    int? RestingHeartRate);

public sealed record BodyMetricResponse(
    Guid Id,
    DateTimeOffset LoggedAt,
    decimal WeightKg,
    decimal Bmi,
    string BmiCategory,
    decimal? BodyFatPercent,
    decimal? WaistCircumferenceCm,
    int? SystolicBloodPressure,
    int? DiastolicBloodPressure,
    int? RestingHeartRate);

public sealed record MetricsHistoryResponse(
    IReadOnlyList<BodyMetricResponse> Items,
    int PageNumber,
    int PageSize,
    int TotalCount,
    int TotalPages);
