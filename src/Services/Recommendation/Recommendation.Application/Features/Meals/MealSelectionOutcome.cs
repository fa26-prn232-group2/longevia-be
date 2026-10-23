namespace Recommendation.Application.Features.Meals;

/// <summary>
/// Kết quả bước chọn món bằng mô hình. <see cref="Abstained"/> = mô hình lỗi,
/// hết thời gian chờ hoặc không chọn được món nào trong danh mục.
/// </summary>
public sealed record MealSelectionOutcome
{
    /// <summary>Các món đã chọn trong danh mục đã lọc.</summary>
    public IReadOnlyList<MealSuggestion> Meals { get; init; } = [];

    /// <summary>Không có món nào được chọn.</summary>
    public bool Abstained { get; init; }

    /// <summary>Lý do abstain (ghi log/telemetry, không hiển thị nguyên văn cho người dùng).</summary>
    public string? AbstainReason { get; init; }
}
