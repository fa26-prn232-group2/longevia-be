namespace Recommendation.Domain.Models;

/// <summary>
/// Hồ sơ người dùng đi kèm request. Theo Constitution §I, hồ sơ **không** được
/// lưu vào kho tri thức — kiểu này chỉ tồn tại trong luồng gọi.
/// </summary>
public sealed record UserProfile
{
    /// <summary>Tuổi.</summary>
    public int Age { get; init; }

    /// <summary>Giới sinh học: <c>MALE</c> hoặc <c>FEMALE</c>.</summary>
    public string Sex { get; init; } = string.Empty;

    /// <summary>Chiều cao (cm).</summary>
    public double HeightCm { get; init; }

    /// <summary>Cân nặng (kg).</summary>
    public double WeightKg { get; init; }

    /// <summary>Mức vận động: Sedentary | LightlyActive | ModeratelyActive | VeryActive.</summary>
    public string ActivityLevel { get; init; } = string.Empty;

    /// <summary>Sở thích ăn uống: None | Keto | Vegan | Mediterranean | LowCarb.</summary>
    public string DietaryPreference { get; init; } = string.Empty;

    /// <summary>Danh sách thành phần gây dị ứng (đã chuẩn hoá lowercase).</summary>
    public IReadOnlyList<string> Allergies { get; init; } = [];
}
