namespace Recommendation.Domain.Constants;

/// <summary>
/// Bữa ăn mà caller xin gợi ý (FR-007). Giá trị enum map 1-1 với
/// <c>MealType</c> trong contract gRPC; <c>Unspecified</c> chỉ dùng khi caller
/// không gửi bữa cụ thể.
/// </summary>
public enum MealSlot
{
    /// <summary>Caller không chỉ định bữa.</summary>
    Unspecified = 0,

    /// <summary>Bữa sáng.</summary>
    Breakfast = 1,

    /// <summary>Bữa trưa.</summary>
    Lunch = 2,

    /// <summary>Bữa tối.</summary>
    Dinner = 3,

    /// <summary>Bữa phụ.</summary>
    Snack = 4,
}
