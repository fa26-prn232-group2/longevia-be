namespace Recommendation.IntegrationTests.Features.Shared;

/// <summary>
/// Một đoạn corpus thật cần nạp cho integration test: nội dung lấy từ
/// <c>docs/reference/che-do-an-truong-tho/</c> kèm đúng số trang bản in/scan.
/// </summary>
internal sealed record SeedChunk
{
    internal required string Content { get; init; }

    internal required int BookPageFrom { get; init; }

    internal int BookPageTo { get; init; }

    internal required int PdfPageFrom { get; init; }

    internal int PdfPageTo { get; init; }

    internal bool Verified { get; init; }

    internal bool NeedsReview { get; init; }
}
