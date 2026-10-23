namespace Recommendation.Infrastructure.Persistence.Entities;

/// <summary>
/// Một chương của corpus (<c>kb.chapter</c>). Số trang lưu cả trang bản in
/// (<c>book_page_*</c>) và trang file scan (<c>pdf_page_*</c>) vì hai hệ số khác nhau.
/// </summary>
public sealed class Chapter
{
    /// <summary>Khoá chính (uuid).</summary>
    public Guid Id { get; set; }

    /// <summary>Corpus sở hữu chương.</summary>
    public Guid CorpusId { get; set; }

    /// <summary>Số thứ tự chương; <c>0</c> là phần đầu sách.</summary>
    public int ChapterNumber { get; set; }

    /// <summary>Tiêu đề chương.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Tên file Markdown nguồn.</summary>
    public string SourceFile { get; set; } = string.Empty;

    /// <summary>Trang bản in đầu tiên của chương.</summary>
    public int BookPageFrom { get; set; }

    /// <summary>Trang bản in cuối cùng của chương.</summary>
    public int BookPageTo { get; set; }

    /// <summary>Trang file scan đầu tiên của chương.</summary>
    public int PdfPageFrom { get; set; }

    /// <summary>Trang file scan cuối cùng của chương.</summary>
    public int PdfPageTo { get; set; }

    /// <summary>Corpus sở hữu chương.</summary>
    public Corpus? Corpus { get; set; }

    /// <summary>Các đoạn thuộc chương.</summary>
    public ICollection<Chunk> Chunks { get; set; } = new List<Chunk>();
}
