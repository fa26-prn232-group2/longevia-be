namespace Recommendation.Infrastructure.Adapters.Knowledge;

/// <summary>
/// Kết quả một dòng của truy vấn hybrid (vector + keyword, merge bằng RRF).
/// Là keyless entity chỉ dùng cho <c>FromSqlRaw</c>; SQL trả về projection,
/// KHÔNG select cột <c>embedding</c> (D-14).
/// </summary>
public sealed class ChunkSearchResult
{
    /// <summary>Khoá của đoạn trong <c>kb.chunk</c>.</summary>
    public long Id { get; set; }

    /// <summary>Corpus sở hữu đoạn.</summary>
    public Guid CorpusId { get; set; }

    /// <summary>Nội dung đoạn.</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>Số chương (0 = phần đầu sách).</summary>
    public int ChapterNumber { get; set; }

    /// <summary>Tiêu đề chương.</summary>
    public string ChapterTitle { get; set; } = string.Empty;

    /// <summary>Trang bản in đầu tiên của đoạn.</summary>
    public int BookPageFrom { get; set; }

    /// <summary>Trang bản in cuối cùng của đoạn.</summary>
    public int BookPageTo { get; set; }

    /// <summary>Trang file scan đầu tiên của đoạn.</summary>
    public int PdfPageFrom { get; set; }

    /// <summary>Trang file scan cuối cùng của đoạn.</summary>
    public int PdfPageTo { get; set; }

    /// <summary>Đoạn đã được đối chiếu bản in.</summary>
    public bool Verified { get; set; }

    /// <summary>Đoạn còn cần rà soát.</summary>
    public bool NeedsReview { get; set; }

    /// <summary>Tên file Markdown nguồn của chương (dùng cho citation).</summary>
    public string SourceFile { get; set; } = string.Empty;

    /// <summary>Điểm gộp RRF.</summary>
    public double RrfScore { get; set; }
}
