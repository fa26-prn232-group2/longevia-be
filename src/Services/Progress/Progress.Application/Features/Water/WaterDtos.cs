namespace Progress.Application.Features.Water;

public sealed record LogWaterRequest(int AmountMl);

public sealed record WaterSummaryResponse(
    DateOnly Date,
    int ConsumedMl,
    int TargetMl,
    double PercentComplete);
