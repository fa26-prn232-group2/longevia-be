using Recommendation.Domain.Constants;
using Recommendation.Domain.Models;

namespace Recommendation.Application.Features.Meals;

/// <summary>
/// Đầu vào của use case gợi ý bữa ăn (Application DTO). Contract gRPC được map
/// sang kiểu này ở tầng Api; Application không tham chiếu tới generated code.
/// </summary>
public sealed record MealRecommendationRequest
{
    /// <summary>Bữa cần gợi ý.</summary>
    public required MealSlot MealSlot { get; init; }

    /// <summary>Mục tiêu người dùng (LOSE_WEIGHT | MAINTAIN | BUILD_MUSCLE | LONGEVITY).</summary>
    public string Goal { get; init; } = string.Empty;

    /// <summary>Hồ sơ người dùng của lời gọi này — không được lưu lại.</summary>
    public required UserProfile Profile { get; init; }

    /// <summary>Calo đã ăn trong ngày; dùng cho rule 105% (FR-004).</summary>
    public int ConsumedCalories { get; init; }

    /// <summary>Mục tiêu calo của ngày.</summary>
    public int TargetCalories { get; init; }

    /// <summary>Protein còn thiếu so với mục tiêu (g).</summary>
    public double TargetProteinRemaining { get; init; }

    /// <summary>Carbohydrate còn thiếu so với mục tiêu (g).</summary>
    public double TargetCarbsRemaining { get; init; }

    /// <summary>Chất béo còn thiếu so với mục tiêu (g).</summary>
    public double TargetFatRemaining { get; init; }

    /// <summary>Danh mục món an toàn do caller gửi — nguồn duy nhất được phép chọn (FR-001).</summary>
    public IReadOnlyList<CandidateFood> CandidateFoods { get; init; } = [];

    /// <summary>Ngôn ngữ mong muốn của câu trả lời.</summary>
    public string Locale { get; init; } = "vi-VN";
}
