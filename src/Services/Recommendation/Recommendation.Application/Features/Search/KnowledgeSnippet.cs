namespace Recommendation.Application.Features.Search;

/// <summary>
/// Một đoạn sách trả cho người dùng khi tra cứu: kèm chương, trang bản in, trang
/// scan, điểm liên quan và trạng thái đối chiếu bản in (FR-020).
/// </summary>
public sealed record KnowledgeSnippet
{
    /// <summary>Số chương (0 = phần đầu sách).</summary>
    public required int ChapterNumber { get; init; }

    /// <summary>Tiêu đề chương; rỗng khi kho không trả tiêu đề.</summary>
    public string ChapterTitle { get; init; } = string.Empty;

    /// <summary>Trang bản in bắt đầu của đoạn - nguồn chuẩn để người dùng tự tra.</summary>
    public required int BookPage { get; init; }

    /// <summary>Trang file scan bắt đầu của đoạn - dùng để đối chiếu.</summary>
    public required int PdfPage { get; init; }

    /// <summary>Nội dung đoạn.</summary>
    public required string Text { get; init; }

    /// <summary>Điểm liên quan (RRF); lớn hơn là liên quan hơn.</summary>
    public required double Score { get; init; }

    /// <summary>Đoạn đã được đối chiếu với bản in.</summary>
    public bool Verified { get; init; }
}
