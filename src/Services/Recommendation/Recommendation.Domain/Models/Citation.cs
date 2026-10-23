namespace Recommendation.Domain.Models;

/// <summary>
/// Trích dẫn tới một đoạn sách: chương + trang bản in (nguồn chuẩn) + trang scan
/// + trích đoạn nguyên văn + trạng thái đối chiếu bản in.
/// </summary>
public sealed record Citation
{
    /// <summary>Số chương (0 = phần đầu sách).</summary>
    public required int ChapterNumber { get; init; }

    /// <summary>Tiêu đề chương.</summary>
    public required string ChapterTitle { get; init; }

    /// <summary>Trang bản in — nguồn chuẩn để người dùng tự mở sách tra.</summary>
    public required int BookPage { get; init; }

    /// <summary>Trang file scan (đối chiếu).</summary>
    public required int PdfPage { get; init; }

    /// <summary>Trích đoạn nguyên văn, đã đối chiếu khớp nội dung đoạn nguồn.</summary>
    public required string Quote { get; init; }

    /// <summary>Trang đã được đối chiếu với bản in hay chưa.</summary>
    public required bool Verified { get; init; }

    /// <summary>Tệp Markdown nguồn của chương.</summary>
    public required string SourceFile { get; init; }
}
