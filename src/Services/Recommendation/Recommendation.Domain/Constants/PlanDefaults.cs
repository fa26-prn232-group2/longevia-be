namespace Recommendation.Domain.Constants;

/// <summary>
/// Giới hạn và tỷ lệ dùng chung cho hai RPC plan (D-04/D-05, data-model §10).
/// Toàn bộ là dữ liệu thuần để tầng Application chỉ việc áp dụng.
/// </summary>
public static class PlanDefaults
{
    /// <summary>Số món ứng viên tối đa trước khi gọi AI; vượt thì cắt theo macro-fit.</summary>
    public const int MaxCandidateFoods = 120;

    /// <summary>Số món tối đa trong một bữa.</summary>
    public const int MaxFoodsPerMeal = 5;

    /// <summary>Số ngày tối thiểu của một kế hoạch.</summary>
    public const int MinDays = 1;

    /// <summary>Số ngày tối đa của một kế hoạch.</summary>
    public const int MaxDays = 7;

    /// <summary>Số bữa tối thiểu mỗi ngày.</summary>
    public const int MinMealsPerDay = 1;

    /// <summary>Số bữa tối đa mỗi ngày.</summary>
    public const int MaxMealsPerDay = 4;

    /// <summary>Mặc định số buổi/tuần khi caller gửi 0: <c>min(3, days)</c>.</summary>
    public const int DefaultSessionsPerWeek = 3;

    /// <summary>Mặc định số phút mỗi buổi khi caller gửi 0.</summary>
    public const int DefaultMinutesPerSession = 30;

    /// <summary>Số phút tối thiểu mỗi buổi khi caller gửi giá trị khác 0.</summary>
    public const int MinMinutesPerSession = 10;

    /// <summary>Số phút tối đa mỗi buổi.</summary>
    public const int MaxMinutesPerSession = 120;

    /// <summary>Số suggestion tối đa cho một ngày chạm trần calo.</summary>
    public const int MaxSuggestionsPerDay = 3;

    /// <summary>Sai số cho phép của calo/protein/carbs/fat mỗi ngày (10%).</summary>
    public const double DailyTargetTolerance = 0.10;

    /// <summary>Ngưỡng chạm trần calo so với mục tiêu ngày (105%).</summary>
    public const double CalorieCapRatio = 1.05;

    /// <summary>
    /// Tỷ lệ chia ngân sách ngày cho các bữa theo <paramref name="mealsPerDay"/>
    /// (D-04). Tổng tỷ lệ luôn bằng 1; caller/validator chịu trách nhiệm bảo đảm
    /// <paramref name="mealsPerDay"/> nằm trong 1..4 trước khi gọi.
    /// </summary>
    public static IReadOnlyList<(MealSlot Slot, double Ratio)> MealShares(int mealsPerDay) =>
        mealsPerDay switch
        {
            1 => [(MealSlot.Lunch, 1.00)],
            2 => [(MealSlot.Lunch, 0.40), (MealSlot.Dinner, 0.60)],
            3 =>
            [
                (MealSlot.Breakfast, 0.30),
                (MealSlot.Lunch, 0.40),
                (MealSlot.Dinner, 0.30),
            ],
            4 =>
            [
                (MealSlot.Breakfast, 0.25),
                (MealSlot.Lunch, 0.35),
                (MealSlot.Dinner, 0.30),
                (MealSlot.Snack, 0.10),
            ],
            _ => throw new ArgumentOutOfRangeException(
                nameof(mealsPerDay),
                mealsPerDay,
                "Số bữa mỗi ngày phải nằm trong khoảng 1..4."),
        };
}
