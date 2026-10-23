namespace Recommendation.Domain.Constants;

/// <summary>
/// Nhãn hiển thị của <see cref="MealSlot"/> dùng trong prompt và trong truy vấn
/// tri thức. Không hard-code chuỗi bữa ở tầng Application.
/// </summary>
public static class MealSlotLabels
{
    /// <summary>Nhãn tiếng Việt để mô hình trả lời tự nhiên.</summary>
    public static string Vietnamese(MealSlot slot) => slot switch
    {
        MealSlot.Breakfast => "bữa sáng",
        MealSlot.Lunch => "bữa trưa",
        MealSlot.Dinner => "bữa tối",
        MealSlot.Snack => "bữa phụ",
        _ => "bữa ăn",
    };

    /// <summary>Tên kỹ thuật khớp enum <c>MealType</c> trong contract gRPC.</summary>
    public static string ContractName(MealSlot slot) => slot.ToString().ToUpperInvariant();
}
