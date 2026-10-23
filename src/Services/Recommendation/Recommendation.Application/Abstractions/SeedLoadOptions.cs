namespace Recommendation.Application.Abstractions;

/// <summary>
/// Tuỳ chọn cho một lần nạp seed tri thức (<c>--bootstrap</c> của Recommendation.Api).
/// </summary>
public sealed class SeedLoadOptions
{
    /// <summary>
    /// Thư mục chứa seed đã xuất: <c>build.json</c>, <c>chunks.jsonl.gz</c> và
    /// <c>page_flags.jsonl.gz</c>. Mặc định <c>/seed</c> (volume trong compose).
    /// </summary>
    public string SeedPath { get; init; } = "/seed";

    /// <summary>
    /// Khi <c>true</c>, bỏ qua toàn bộ thao tác ghi nếu đúng <c>(corpus_key, version)</c>
    /// đang active. Dùng cho deploy lặp lại mà không muốn nạp lại corpus.
    /// </summary>
    public bool SkipIfLoaded { get; init; }

    /// <summary>
    /// Khi <c>true</c>, đối chiếu database sau khi nạp (hoặc sau khi bỏ qua) với
    /// metadata của seed và fail nếu không khớp.
    /// </summary>
    public bool Verify { get; init; }
}
