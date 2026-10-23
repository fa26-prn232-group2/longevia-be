namespace Recommendation.Infrastructure.Persistence.Entities;

/// <summary>
/// Một phiên bản corpus tri thức đã nạp vào kho (<c>kb.corpus</c>).
/// Theo Constitution §I, bảng này chỉ chứa dữ liệu tri thức, không có dữ liệu người dùng.
/// </summary>
public sealed class Corpus
{
    /// <summary>Khoá chính (uuid).</summary>
    public Guid Id { get; set; }

    /// <summary>Định danh corpus, ví dụ <c>che-do-an-truong-tho</c>.</summary>
    public string CorpusKey { get; set; } = string.Empty;

    /// <summary>Phiên bản nội dung (SHA-256 của tập file Markdown).</summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>Tên hiển thị của corpus.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Đường dẫn nguồn trong repository.</summary>
    public string SourcePath { get; set; } = string.Empty;

    /// <summary>Số chương của corpus.</summary>
    public int ChapterCount { get; set; }

    /// <summary>Số đoạn (chunk) của corpus.</summary>
    public int ChunkCount { get; set; }

    /// <summary>Model dùng để nhúng; phải khớp <see cref="EmbeddingDim"/> và cột <c>vector(n)</c>.</summary>
    public string EmbeddingModel { get; set; } = string.Empty;

    /// <summary>Số chiều vector; phải khớp cột <c>kb.chunk.embedding</c>.</summary>
    public int EmbeddingDim { get; set; }

    /// <summary>Tham số chunker dạng JSON, ví dụ <c>{"tokens":1000,"overlap":150}</c>.</summary>
    public string ChunkerParams { get; set; } = "{}";

    /// <summary>Chỉ một phiên bản active cho mỗi <see cref="CorpusKey"/>.</summary>
    public bool IsActive { get; set; }

    /// <summary>Thời điểm hoàn tất nhúng corpus.</summary>
    public DateTimeOffset EmbeddedAt { get; set; }

    /// <summary>Thời điểm tạo bản ghi.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Các chương thuộc corpus.</summary>
    public ICollection<Chapter> Chapters { get; set; } = new List<Chapter>();

    /// <summary>Các đoạn thuộc corpus.</summary>
    public ICollection<Chunk> Chunks { get; set; } = new List<Chunk>();

    /// <summary>Các cờ chất lượng theo trang của corpus.</summary>
    public ICollection<PageFlag> PageFlags { get; set; } = new List<PageFlag>();
}
