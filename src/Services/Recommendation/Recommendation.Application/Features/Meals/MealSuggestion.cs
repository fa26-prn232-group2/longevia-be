using Recommendation.Domain.Models;

namespace Recommendation.Application.Features.Meals;

/// <summary>
/// Một món được gợi ý. Calo và macro **luôn** lấy từ <see cref="CandidateFood"/>
/// đã chọn (FR-005), không do mô hình sinh ra; mô hình chỉ chọn món và viết
/// <see cref="Rationale"/> kèm trích dẫn.
/// </summary>
public sealed record MealSuggestion
{
    /// <summary>Định danh món trong danh mục caller; rỗng với gợi ý ngoài danh mục (đồ uống 0 calo).</summary>
    public string FoodId { get; init; } = string.Empty;

    /// <summary>Tên món lấy từ danh mục.</summary>
    public required string Name { get; init; }

    /// <summary>Calo lấy từ danh mục (kcal).</summary>
    public int EstimatedCalories { get; init; }

    /// <summary>Protein lấy từ danh mục (g).</summary>
    public double ProteinGrams { get; init; }

    /// <summary>Carbohydrate lấy từ danh mục (g).</summary>
    public double CarbsGrams { get; init; }

    /// <summary>Chất béo lấy từ danh mục (g).</summary>
    public double FatGrams { get; init; }

    /// <summary>Lý do chọn món (FR-006).</summary>
    public string Rationale { get; init; } = string.Empty;

    /// <summary>Trích dẫn đã kiểm chứng khớp nội dung đoạn nguồn; rỗng khi corpus chưa có căn cứ.</summary>
    public IReadOnlyList<Citation> Citations { get; init; } = [];
}
