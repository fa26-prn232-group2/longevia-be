using Recommendation.Domain.Constants;

namespace Recommendation.Domain.Models;

/// <summary>
/// Ngân sách calo/macro/fiber đã chia cho từng ngày và từng bữa (D-04). Model
/// nội bộ chỉ tồn tại trong một lời gọi, không được lưu.
/// </summary>
public sealed record WeeklyBudget
{
    /// <summary>Các ngày của kế hoạch, theo đúng thứ tự ngày liên tiếp.</summary>
    public required IReadOnlyList<DayBudget> Days { get; init; }

    /// <summary>Ngân sách của một ngày và danh sách ngân sách bữa trong ngày.</summary>
    public sealed record DayBudget
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

        /// <summary>Ngân sách chất xơ của ngày (g); <c>0</c> khi caller không gửi.</summary>
        public double TargetFiberGrams { get; init; }

        /// <summary>Ngân sách các bữa trong ngày; tổng phải bằng ngân sách ngày.</summary>
        public required IReadOnlyList<MealBudget> Meals { get; init; }
    }

    /// <summary>Ngân sách của một bữa trong ngày.</summary>
    public sealed record MealBudget
    {
        /// <summary>Loại bữa theo <see cref="MealSlot"/>.</summary>
        public required MealSlot Slot { get; init; }

        /// <summary>Ngân sách calo của bữa (kcal).</summary>
        public int TargetCalories { get; init; }

        /// <summary>Ngân sách protein của bữa (g).</summary>
        public double TargetProteinGrams { get; init; }

        /// <summary>Ngân sách carbohydrate của bữa (g).</summary>
        public double TargetCarbsGrams { get; init; }

        /// <summary>Ngân sách chất béo của bữa (g).</summary>
        public double TargetFatGrams { get; init; }

        /// <summary>Ngân sách chất xơ của bữa (g); <c>0</c> khi caller không gửi.</summary>
        public double TargetFiberGrams { get; init; }
    }
}
