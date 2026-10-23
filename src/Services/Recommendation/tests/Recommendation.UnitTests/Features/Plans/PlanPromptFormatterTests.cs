using Recommendation.Application.Features.Plans;
using Recommendation.Domain.Models;

namespace Recommendation.UnitTests.Features.Plans;

/// <summary>
/// T017 — corpus luôn được đóng khung là dữ liệu, mỗi đoạn gắn trạng thái
/// VERIFIED/NEEDS_REVIEW và chỉ chứa nội dung của chính đoạn nguồn.
/// </summary>
public sealed class PlanPromptFormatterTests
{
    private const string VerifiedContent =
        "Chế độ ăn trường thọ ưu tiên rau, đậu và ngũ cốc nguyên hạt.";

    private const string ReviewContent =
        "Tập thể dục đều đặn giúp duy trì khối cơ và sức bền.";

    private readonly PlanPromptFormatter _formatter = new();

    [Fact]
    public void WrapsChunksBetweenCorpusMarkers()
    {
        var formatted = _formatter.FormatCorpus([Chunk(VerifiedContent, verified: true)]);

        Assert.StartsWith("[BEGIN CORPUS DATA]", formatted, StringComparison.Ordinal);
        Assert.EndsWith("[END CORPUS DATA]", formatted, StringComparison.Ordinal);
    }

    [Fact]
    public void MarksVerifiedAndNeedsReviewChunks()
    {
        var formatted = _formatter.FormatCorpus(
            [Chunk(VerifiedContent, verified: true), Chunk(ReviewContent, verified: false)]);

        Assert.Contains("status=VERIFIED", formatted, StringComparison.Ordinal);
        Assert.Contains("status=NEEDS_REVIEW", formatted, StringComparison.Ordinal);
    }

    [Fact]
    public void IncludesOnlyProvidedChunkContent()
    {
        var formatted = _formatter.FormatCorpus([Chunk(VerifiedContent, verified: true)]);

        Assert.Contains(VerifiedContent, formatted, StringComparison.Ordinal);
        Assert.DoesNotContain(ReviewContent, formatted, StringComparison.Ordinal);
    }

    [Fact]
    public void ReturnsEmptyForEmptyCorpus()
    {
        Assert.Equal(string.Empty, _formatter.FormatCorpus([]));
    }

    private static RetrievedChunk Chunk(string content, bool verified) => new()
    {
        Chunk = new KnowledgeChunk
        {
            CorpusId = Guid.NewGuid(),
            ChapterNumber = verified ? 4 : 5,
            ChapterTitle = verified ? "CHẾ ĐỘ ĂN TRƯỜNG THỌ" : "TẬP THỂ DỤC",
            Content = content,
            BookPageFrom = verified ? 72 : 90,
            BookPageTo = verified ? 72 : 90,
            PdfPageFrom = verified ? 64 : 82,
            PdfPageTo = verified ? 64 : 82,
            Verified = verified,
            NeedsReview = !verified,
            SourceFile = verified
                ? "04-chuong-4-che-do-an-truong-tho.md"
                : "05-chuong-5-tap-the-duc-va-song-lau-khoe-manh.md",
        },
        Score = 0.9,
    };
}
