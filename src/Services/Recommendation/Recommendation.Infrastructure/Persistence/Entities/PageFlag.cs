namespace Recommendation.Infrastructure.Persistence.Entities;

/// <summary>
/// Cờ chất lượng theo trang (<c>kb.page_flag</c>) — nguồn sự thật để suy ra
/// cờ <c>verified</c> / <c>needs_review</c> của từng đoạn.
/// </summary>
public sealed class PageFlag
{
    /// <summary>Khoá chính (bigserial).</summary>
    public long Id { get; set; }

    /// <summary>Corpus sở hữu cờ.</summary>
    public Guid CorpusId { get; set; }

    /// <summary>Trang trong file scan — khoá của các file <c>review/*.md</c>.</summary>
    public int PdfPage { get; set; }

    /// <summary>Trang bản in tương ứng.</summary>
    public int BookPage { get; set; }

    /// <summary>Loại cờ, ví dụ <c>manual_verified</c>, <c>missing_numbers</c>.</summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>File nguồn của cờ, ví dụ <c>review/qa.md</c>.</summary>
    public string SourceFile { get; set; } = string.Empty;

    /// <summary>Ghi chú thêm (không bắt buộc).</summary>
    public string? Note { get; set; }

    /// <summary>Corpus sở hữu cờ.</summary>
    public Corpus? Corpus { get; set; }
}
