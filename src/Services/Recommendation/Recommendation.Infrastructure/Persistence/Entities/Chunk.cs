using NpgsqlTypes;
using Pgvector;

namespace Recommendation.Infrastructure.Persistence.Entities;

/// <summary>
/// Đơn vị truy xuất của kho tri thức (<c>kb.chunk</c>).
/// Cột <see cref="ContentTsv"/> do trigger trong migration duy trì, còn
/// <see cref="Embedding"/> là vector L2-normalized dùng cho tìm kiếm cosine.
/// </summary>
public sealed class Chunk
{
    /// <summary>Khoá chính (bigserial).</summary>
    public long Id { get; set; }

    /// <summary>Corpus sở hữu đoạn.</summary>
    public Guid CorpusId { get; set; }

    /// <summary>Chương sở hữu đoạn.</summary>
    public Guid ChapterId { get; set; }

    /// <summary>Số chương (khửi hoá từ <c>kb.chapter</c>) để lọc không cần join.</summary>
    public int ChapterNumber { get; set; }

    /// <summary>Thứ tự của đoạn trong chương.</summary>
    public int ChunkIndex { get; set; }

    /// <summary>Nội dung đoạn đã làm sạch OCR.</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Chỉ mục toàn văn của nội dung; giá trị do trigger <c>BEFORE INSERT OR UPDATE OF content</c>
    /// sinh ra nên không gán tay và không được EF ghi xuống.
    /// </summary>
    public NpgsqlTsVector? ContentTsv { get; set; }

    /// <summary>Ước lượng số token của nội dung.</summary>
    public int TokenCount { get; set; }

    /// <summary>SHA-256 của nội dung, dùng cho idempotency và dedupe.</summary>
    public string ContentHash { get; set; } = string.Empty;

    /// <summary>Trang bản in đầu tiên của đoạn (nguồn chuẩn để người dùng tự tra).</summary>
    public int BookPageFrom { get; set; }

    /// <summary>Trang bản in cuối cùng của đoạn.</summary>
    public int BookPageTo { get; set; }

    /// <summary>Trang file scan đầu tiên của đoạn.</summary>
    public int PdfPageFrom { get; set; }

    /// <summary>Trang file scan cuối cùng của đoạn.</summary>
    public int PdfPageTo { get; set; }

    /// <summary>Đoạn đã được đối chiếu với bản in.</summary>
    public bool Verified { get; set; }

    /// <summary>Đoạn còn cần người rà soát (số liệu có thể sai do OCR).</summary>
    public bool NeedsReview { get; set; }

    /// <summary>Vector nhúng L2-normalized của nội dung.</summary>
    public required Vector Embedding { get; set; }

    /// <summary>Thời điểm tạo bản ghi.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Corpus sở hữu đoạn.</summary>
    public Corpus? Corpus { get; set; }

    /// <summary>Chương sở hữu đoạn.</summary>
    public Chapter? Chapter { get; set; }
}
