using Recommendation.Domain.Models;

namespace Recommendation.Application.Features.MealPlans;

/// <summary>
/// DTO Application của <c>GenerateMealPlan</c> (data-model §2/FR-038/FR-039).
/// Contract gRPC được map sang kiểu này ở tầng Api; mọi mục tiêu calo/macro là
/// <b>mục tiêu ngày</b>, use case nhân với <see cref="Days"/> để có tổng tuần.
/// </summary>
public sealed record GenerateMealPlanRequest
{
    /// <summary>Hồ sơ người dùng của lời gọi này — không được lưu lại.</summary>
    public UserProfile Profile { get; init; } = new();

    /// <summary>Mục tiêu calo của một ngày; phải lớn hơn 0.</summary>
    public int TargetCalories { get; init; }

    /// <summary>Mục tiêu protein của một ngày (g).</summary>
    public double TargetProteinGrams { get; init; }

    /// <summary>Mục tiêu carbohydrate của một ngày (g).</summary>
    public double TargetCarbsGrams { get; init; }

    /// <summary>Mục tiêu chất béo của một ngày (g).</summary>
    public double TargetFatGrams { get; init; }

    /// <summary>Mục tiêu chất xơ của một ngày (g); <c>0</c> nghĩa là caller không gửi.</summary>
    public double TargetFiberGrams { get; init; }

    /// <summary>Ngày bắt đầu tuần ISO <c>YYYY-MM-DD</c>, phải là thứ Hai.</summary>
    public string WeekStartDate { get; init; } = string.Empty;

    /// <summary>Số ngày cần lập kế hoạch (1..7).</summary>
    public int Days { get; init; }

    /// <summary>Số bữa mỗi ngày (1..4).</summary>
    public int MealsPerDay { get; init; }

    /// <summary>Danh mục món an toàn do caller gửi — nguồn chọn món duy nhất.</summary>
    public IReadOnlyList<CandidateFood> CandidateFoods { get; init; } = [];

    /// <summary>Các <c>food_id</c> bị loại khỏi mọi ngày.</summary>
    public IReadOnlyList<string> ExcludedFoodIds { get; init; } = [];

    /// <summary>Calo đã ăn của ngày <see cref="ConsumedDate"/>; <c>0</c> nghĩa là không gửi.</summary>
    public int ConsumedCalories { get; init; }

    /// <summary>Ngày ISO <c>YYYY-MM-DD</c> mà <see cref="ConsumedCalories"/> thuộc về.</summary>
    public string? ConsumedDate { get; init; }

    /// <summary>Ngôn ngữ mong muốn; chỉ <c>vi-VN</c> được hỗ trợ.</summary>
    public string Locale { get; init; } = string.Empty;
}
