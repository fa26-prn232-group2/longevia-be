namespace Recommendation.Application.Features.Meals;

/// <summary>
/// Rule chạm trần calo (FR-004): khi calo đã ăn đạt từ 105% mục tiêu trở lên
/// thì không gợi ý bữa ăn chính, chỉ đồ uống 0 calo cùng mẹo thư giãn/giấc ngủ.
/// </summary>
public sealed class CalorieCapRule
{
    /// <summary>Tỷ lệ chặn: 105% mục tiêu calo ngày.</summary>
    public const double CapRatio = 1.05;

    private const string ZeroCalorieDrinkName = "Trà thảo mộc không đường";

    private const string ZeroCalorieDrinkRationale =
        "Đồ uống 0 calo giúp giữ nhịp uống nước mà không cộng thêm năng lượng vào ngày đã chạm trần calo.";

    /// <summary><c>true</c> khi calo đã ăn đạt từ 105% mục tiêu; mục tiêu không hợp lệ thì không chặn.</summary>
    public bool IsCapReached(int consumedCalories, int targetCalories)
    {
        if (targetCalories <= 0)
        {
            return false;
        }

        // So sánh số nguyên để đúng ngay tại biên 105% (tránh sai số dấu phẩy động).
        return (long)consumedCalories * 100 >= (long)targetCalories * 105;
    }

    /// <summary>Gợi ý thay thế khi đã chạm trần: đồ uống 0 calo, không thuộc danh mục món.</summary>
    public MealSuggestion CreateZeroCalorieSuggestion() => new()
    {
        FoodId = string.Empty,
        Name = ZeroCalorieDrinkName,
        EstimatedCalories = 0,
        Rationale = ZeroCalorieDrinkRationale,
    };
}
