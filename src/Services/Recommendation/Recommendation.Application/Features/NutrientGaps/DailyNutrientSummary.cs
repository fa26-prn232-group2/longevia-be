namespace Recommendation.Application.Features.NutrientGaps;

/// <summary>
/// Tổng hợp dinh dưỡng của một ngày do bên gọi gửi kèm (Application DTO). Chỉ
/// tồn tại trong luồng gọi, không được ghi vào kho tri thức.
/// </summary>
public sealed record DailyNutrientSummary
{
    /// <summary>Ngày của bản tổng hợp (chuỗi ISO-8601 do bên gọi quy định).</summary>
    public string Date { get; init; } = string.Empty;

    /// <summary>Năng lượng đã nạp (kcal).</summary>
    public int Calories { get; init; }

    /// <summary>Protein đã nạp (g).</summary>
    public double Protein { get; init; }

    /// <summary>Carbohydrate đã nạp (g).</summary>
    public double Carbs { get; init; }

    /// <summary>Chất béo đã nạp (g).</summary>
    public double Fat { get; init; }

    /// <summary>Chất xơ đã nạp (g).</summary>
    public double Fiber { get; init; }

    /// <summary>
    /// Vi chất đã nạp trong ngày; bên gọi quyết định gửi vi chất nào. Không gửi
    /// một vi chất nghĩa là "chưa có dữ liệu", **không** phải "đã nạp 0".
    /// </summary>
    public IReadOnlyList<MicronutrientIntake> Micronutrients { get; init; } = [];
}
