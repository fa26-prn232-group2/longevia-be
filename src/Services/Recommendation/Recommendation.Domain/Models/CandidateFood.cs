namespace Recommendation.Domain.Models;

/// <summary>
/// Món ăn trong danh mục an toàn do bên gọi gửi kèm. Mô hình **không** được
/// sinh món ngoài danh mục này.
/// </summary>
public sealed record CandidateFood
{
    /// <summary>Khoá món, map về danh mục của service dinh dưỡng.</summary>
    public required string FoodId { get; init; }

    /// <summary>Tên món.</summary>
    public required string Name { get; init; }

    /// <summary>Khối lượng khẩu phần (số).</summary>
    public string ServingSize { get; init; } = string.Empty;

    /// <summary>Đơn vị khẩu phần: g | ml | chén | đĩa.</summary>
    public string ServingUnit { get; init; } = string.Empty;

    /// <summary>Năng lượng (kcal) của khẩu phần.</summary>
    public int Calories { get; init; }

    /// <summary>Protein (g).</summary>
    public double ProteinGrams { get; init; }

    /// <summary>Carbohydrate (g).</summary>
    public double CarbsGrams { get; init; }

    /// <summary>Chất béo (g).</summary>
    public double FatGrams { get; init; }

    /// <summary>Chất xơ (g).</summary>
    public double FiberGrams { get; init; }

    /// <summary>Thành phần gây dị ứng (đã chuẩn hoá lowercase).</summary>
    public IReadOnlyList<string> Allergens { get; init; } = [];
}
