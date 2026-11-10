namespace Progress.Application.Features.Water;

public sealed record LogWaterRequest(int AmountMl);

public sealed record WaterSummaryResponse(
    DateOnly Date,
    int ConsumedMl,
    int TargetMl,
    double PercentComplete);

public sealed record WaterHistoryItemResponse(
    DateOnly Date,
    int ConsumedMl,
    int TargetMl,
    double PercentComplete,
    bool GoalReached);

public sealed record WaterHistoryResponse(
    int Days,
    int TargetMl,
    IReadOnlyList<WaterHistoryItemResponse> Items);
