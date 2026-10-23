namespace Recommendation.Application.Features.NutrientGaps;

/// <summary>
/// Một vi chất đã nạp trong ngày do bên gọi gửi kèm (Application DTO). Tên vi
/// chất do bên gọi quyết định; service chỉ tính khoảng thiếu khi khớp được khoá
/// trong bảng nhu cầu tham chiếu.
/// </summary>
public sealed record MicronutrientIntake
{
    /// <summary>Tên vi chất, ví dụ <c>Vitamin D</c> hoặc <c>Omega-3</c>.</summary>
    public string Nutrient { get; init; } = string.Empty;

    /// <summary>Đơn vị bên gọi dùng: g | mg | mcg (µg) | IU.</summary>
    public string Unit { get; init; } = string.Empty;

    /// <summary>Lượng vi chất đã nạp trong ngày, theo <see cref="Unit"/>.</summary>
    public double Amount { get; init; }
}
