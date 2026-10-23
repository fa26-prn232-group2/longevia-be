using Recommendation.Domain.Constants;
using Recommendation.Domain.Models;

namespace Recommendation.Application.Features.MealPlans;

/// <summary>
/// Kết quả use case <c>GenerateMealPlan</c> (data-model §4). Chỉ là DTO nội bộ
/// tồn tại trong một lời gọi; không được lưu xuống database.
/// </summary>
public sealed record GenerateMealPlanResult
{
    /// <summary>Đúng <see cref="DaysRequested"/> ngày, liên tiếp từ <see cref="WeekStartDate"/>.</summary>
    public IReadOnlyList<MealPlanDay> Days { get; init; } = [];

    /// <summary>Ngày bắt đầu tuần ISO <c>YYYY-MM-DD</c>.</summary>
    public string WeekStartDate { get; init; } = string.Empty;

    /// <summary>Số ngày caller yêu cầu.</summary>
    public int DaysRequested { get; init; }

    /// <summary>Tổng mục tiêu calo tuần = mục tiêu ngày × <see cref="DaysRequested"/>.</summary>
    public int WeeklyTargetCalories { get; init; }

    /// <summary>Tổng mục tiêu protein tuần (g).</summary>
    public double WeeklyTargetProteinGrams { get; init; }

    /// <summary>Tổng mục tiêu carbohydrate tuần (g).</summary>
    public double WeeklyTargetCarbsGrams { get; init; }

    /// <summary>Tổng mục tiêu chất béo tuần (g).</summary>
    public double WeeklyTargetFatGrams { get; init; }

    /// <summary>Tổng mục tiêu chất xơ tuần (g); <c>0</c> khi caller không gửi.</summary>
    public double WeeklyTargetFiberGrams { get; init; }

    /// <summary>Hợp nhất citation đã verify; rỗng khi corpus rỗng.</summary>
    public IReadOnlyList<Citation> Citations { get; init; } = [];

    /// <summary>Cảnh báo cấp plan/ngày.</summary>
    public IReadOnlyList<PlanWarning> Warnings { get; init; } = [];

    /// <summary>Khuyến cáo BR-04 — luôn có mặt.</summary>
    public required string Disclaimer { get; init; }

    /// <summary><c>true</c> khi đã dùng phương án dự phòng xác định.</summary>
    public bool UsedFallback { get; init; }
}

/// <summary>Một ngày của kế hoạch ăn uống (data-model §4.1).</summary>
public sealed record MealPlanDay
{
    /// <summary>Ngày ISO <c>YYYY-MM-DD</c>.</summary>
    public required string Date { get; init; }

    /// <summary>Ngân sách calo của ngày (kcal).</summary>
    public int TargetCalories { get; init; }

    /// <summary>Ngân sách protein của ngày (g).</summary>
    public double TargetProteinGrams { get; init; }

    /// <summary>Ngân sách carbohydrate của ngày (g).</summary>
    public double TargetCarbsGrams { get; init; }

    /// <summary>Ngân sách chất béo của ngày (g).</summary>
    public double TargetFatGrams { get; init; }

    /// <summary>Ngân sách chất xơ của ngày (g).</summary>
    public double TargetFiberGrams { get; init; }

    /// <summary>Tổng calo từ các món đã chọn (kcal).</summary>
    public int TotalCalories { get; init; }

    /// <summary>Tổng protein từ các món (g).</summary>
    public double TotalProteinGrams { get; init; }

    /// <summary>Tổng carbohydrate từ các món (g).</summary>
    public double TotalCarbsGrams { get; init; }

    /// <summary>Tổng chất béo từ các món (g).</summary>
    public double TotalFatGrams { get; init; }

    /// <summary>Tổng chất xơ từ các món (g).</summary>
    public double TotalFiberGrams { get; init; }

    /// <summary>Các bữa của ngày; rỗng khi <see cref="CalorieCapReached"/>.</summary>
    public IReadOnlyList<PlannedMeal> Meals { get; init; } = [];

    /// <summary>Gợi ý thay thế khi chạm trần calo; tối đa 3.</summary>
    public IReadOnlyList<PlanSuggestion> Suggestions { get; init; } = [];

    /// <summary><c>true</c> khi ngày bị rule chạm trần 105%.</summary>
    public bool CalorieCapReached { get; init; }
}

/// <summary>Một bữa trong ngày (data-model §4.2).</summary>
public sealed record PlannedMeal
{
    /// <summary>Loại bữa.</summary>
    public required MealSlot MealType { get; init; }

    /// <summary>Ngân sách calo của bữa (kcal).</summary>
    public int TargetCalories { get; init; }

    /// <summary>Các món được chọn; tối đa 5 và đều thuộc candidate.</summary>
    public IReadOnlyList<PlannedFood> Foods { get; init; } = [];

    /// <summary>Lý do do code dựng từ request + món + citation đã verify.</summary>
    public string Rationale { get; init; } = string.Empty;

    /// <summary>Citation đã verify cho rationale; có thể rỗng.</summary>
    public IReadOnlyList<Citation> Citations { get; init; } = [];
}

/// <summary>Một món trong kế hoạch; sao chép nguyên giá trị từ candidate (data-model §4.3).</summary>
public sealed record PlannedFood
{
    /// <summary>Khoá món trong danh mục caller.</summary>
    public required string FoodId { get; init; }

    /// <summary>Tên món.</summary>
    public required string Name { get; init; }

    /// <summary>Khối lượng khẩu phần.</summary>
    public string ServingSize { get; init; } = string.Empty;

    /// <summary>Đơn vị khẩu phần.</summary>
    public string ServingUnit { get; init; } = string.Empty;

    /// <summary>Năng lượng của khẩu phần (kcal).</summary>
    public int Calories { get; init; }

    /// <summary>Protein (g).</summary>
    public double ProteinGrams { get; init; }

    /// <summary>Carbohydrate (g).</summary>
    public double CarbsGrams { get; init; }

    /// <summary>Chất béo (g).</summary>
    public double FatGrams { get; init; }

    /// <summary>Chất xơ (g).</summary>
    public double FiberGrams { get; init; }

    /// <summary>Thành phần gây dị ứng đã khai.</summary>
    public IReadOnlyList<string> Allergens { get; init; } = [];
}

/// <summary>Gợi ý đồ uống 0 calo/thói quen thư giãn; không mang <c>food_id</c> (data-model §4.4).</summary>
public sealed record PlanSuggestion
{
    /// <summary>Nội dung gợi ý tiếng Việt.</summary>
    public required string Text { get; init; }

    /// <summary>Căn cứ sách đã verify; rỗng khi không có nguồn.</summary>
    public IReadOnlyList<Citation> Citations { get; init; } = [];
}
