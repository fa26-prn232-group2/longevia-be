using Recommendation.Domain.Models;

namespace Recommendation.Application.Features.NutrientGaps;

/// <summary>
/// Kết quả phân tích thiếu hụt vi chất: các khoảng thiếu do code tính (nguồn sự
/// thật), phần diễn giải/thực phẩm gợi ý có căn cứ trong sách kèm trích dẫn, và
/// khuyến cáo bắt buộc.
/// </summary>
public sealed record NutrientGapResult
{
    /// <summary>Các khoảng thiếu đã tính, sắp xếp deterministic (nặng trước).</summary>
    public IReadOnlyList<NutrientGap> Gaps { get; init; } = [];

    /// <summary>Tên các dưỡng chất thiếu, cùng thứ tự với <see cref="Gaps"/>.</summary>
    public IReadOnlyList<string> DeficientNutrients { get; init; } = [];

    /// <summary>Thực phẩm gợi ý đã lọc dị ứng và đối chiếu với nội dung sách.</summary>
    public IReadOnlyList<string> SuggestedFoods { get; init; } = [];

    /// <summary>Phần diễn giải có căn cứ; fallback deterministic khi corpus rỗng.</summary>
    public required string AnalysisSummary { get; init; }

    /// <summary>Trích dẫn đã kiểm chứng khớp nội dung đoạn nguồn.</summary>
    public IReadOnlyList<Citation> Citations { get; init; } = [];

    /// <summary>Khuyến cáo bắt buộc trên mọi phản hồi (FR-027).</summary>
    public required string Disclaimer { get; init; }
}
