using Recommendation.Domain.Models;

namespace Recommendation.Application.Features.Meals;

/// <summary>
/// Kết quả use case gợi ý bữa ăn: tối đa 5 món, mẹo trường thọ, khuyến cáo bắt
/// buộc và cờ chạm trần calo.
/// </summary>
public sealed record MealRecommendationResult
{
    /// <summary>Danh sách món gợi ý (tối đa 5); rỗng khi không có phương án an toàn.</summary>
    public IReadOnlyList<MealSuggestion> Meals { get; init; } = [];

    /// <summary>Mẹo trường thọ lấy từ kho tri thức, fallback sang tip tĩnh.</summary>
    public string LongevityTip { get; init; } = string.Empty;

    /// <summary>Trích dẫn của mẹo trường thọ (rỗng khi dùng tip tĩnh).</summary>
    public IReadOnlyList<Citation> TipCitations { get; init; } = [];

    /// <summary>Khuyến cáo BR-04 — luôn có mặt.</summary>
    public required string Disclaimer { get; init; }

    /// <summary><c>true</c> khi calo đã ăn đạt từ 105% mục tiêu trở lên (FR-004).</summary>
    public bool CalorieCapReached { get; init; }

    /// <summary>Ghi chú an toàn khi không thể gợi ý món (danh mục rỗng, đã lọc hết, provider lỗi).</summary>
    public string SafetyNote { get; init; } = string.Empty;
}
