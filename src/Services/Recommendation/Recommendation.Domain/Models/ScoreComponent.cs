namespace Recommendation.Domain.Models;

/// <summary>
/// Một thành phần trong thang điểm thói quen trường thọ. Tổng trọng số của bốn
/// thành phần là 100 (fasting 35 | sleep 30 | hydration 20 | nutrition 15).
/// </summary>
public sealed record ScoreComponent
{
    /// <summary>Khoá thành phần: <c>fasting</c> | <c>sleep</c> | <c>hydration</c> | <c>nutrition</c>.</summary>
    public required string Key { get; init; }

    /// <summary>Điểm 0–100 của riêng thành phần.</summary>
    public required int Score { get; init; }

    /// <summary>Trọng số phần trăm của thành phần.</summary>
    public required int Weight { get; init; }

    /// <summary>Giải thích ngắn vì sao thành phần đạt điểm đó.</summary>
    public required string Reason { get; init; }
}
