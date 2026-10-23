namespace Recommendation.Domain.Models;

/// <summary>
/// Một đoạn tri thức trong kho (nội bộ). Kiểu này **không** đi qua contract gRPC:
/// chỉ dùng nội bộ giữa kho tri thức và tầng Application.
/// </summary>
public sealed record KnowledgeChunk
{
    /// <summary>Corpus sở hữu đoạn.</summary>
    public required Guid CorpusId { get; init; }

    /// <summary>Chương sở hữu đoạn; rỗng khi kết quả là projection không select cột này.</summary>
    public Guid ChapterId { get; init; }

    /// <summary>Số chương (0 = phần đầu sách).</summary>
    public required int ChapterNumber { get; init; }

    /// <summary>Thứ tự đoạn trong chương; chỉ có ở đường ghi (upsert).</summary>
    public int ChunkIndex { get; init; }

    /// <summary>Nội dung đoạn đã làm sạch OCR.</summary>
    public required string Content { get; init; }

    /// <summary>Ước lượng số token của nội dung; chỉ có ở đường ghi (upsert).</summary>
    public int TokenCount { get; init; }

    /// <summary>SHA-256 của nội dung, dùng cho idempotency và dedupe.</summary>
    public string ContentHash { get; init; } = string.Empty;

    /// <summary>Trang bản in đầu tiên của đoạn (nguồn chuẩn để người dùng tự tra).</summary>
    public required int BookPageFrom { get; init; }

    /// <summary>Trang bản in cuối cùng của đoạn.</summary>
    public required int BookPageTo { get; init; }

    /// <summary>Trang file scan đầu tiên của đoạn.</summary>
    public required int PdfPageFrom { get; init; }

    /// <summary>Trang file scan cuối cùng của đoạn.</summary>
    public required int PdfPageTo { get; init; }

    /// <summary>Đoạn đã được đối chiếu với bản in.</summary>
    public bool Verified { get; init; }

    /// <summary>Đoạn còn cần người rà soát (số liệu có thể sai do OCR).</summary>
    public bool NeedsReview { get; init; }

    /// <summary>
    /// Vector nhúng L2-normalized của nội dung. Rỗng trên đường truy xuất vì
    /// projection không select cột <c>embedding</c> (D-14).
    /// </summary>
    public IReadOnlyList<float> Embedding { get; init; } = [];

    /// <summary>Tên file Markdown nguồn của chương (đi kèm trích dẫn).</summary>
    public string? SourceFile { get; init; }

    /// <summary>Tiêu đề chương (đi kèm trích dẫn).</summary>
    public string? ChapterTitle { get; init; }
}
