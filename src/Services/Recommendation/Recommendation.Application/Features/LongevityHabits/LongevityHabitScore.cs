using Recommendation.Domain.Models;

namespace Recommendation.Application.Features.LongevityHabits;

/// <summary>
/// Kết quả chấm điểm thói quen trường thọ do code tính: điểm tổng 0..100 và bốn
/// thành phần kèm trọng số, lý do.
/// </summary>
public sealed record LongevityHabitScore
{
    /// <summary>Điểm tổng 0..100, là trung bình có trọng số của bốn nhánh.</summary>
    public required int Score { get; init; }

    /// <summary>Bốn thành phần theo thứ tự cố định fasting, sleep, hydration, nutrition.</summary>
    public required IReadOnlyList<ScoreComponent> Components { get; init; }
}
