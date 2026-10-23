using Recommendation.Application.Features.Ask;
using Recommendation.Domain.Models;

namespace Recommendation.UnitTests.Features.Ask;

/// <summary>
/// T079 — D-10/FR-030: prompt phải có đủ 3 tầng (grounding + abstain, unverified
/// guard, corpus-as-data) và đóng khung nội dung sách là dữ liệu, không phải chỉ dẫn.
/// </summary>
public sealed class PromptBuilderTests
{
    private const string Question = "Chất béo không bão hòa đơn có trong thực phẩm nào?";
    private const string ChunkContent =
        "Chất béo không bão hòa đơn có nhiều trong dầu ô liu, các loại hạt và quả bơ.";

    [Fact]
    public void IncludesGroundingAndAbstainLayer()
    {
        var prompt = Build();

        Assert.Contains("TẦNG 1", prompt.SystemInstruction, StringComparison.Ordinal);
        Assert.Contains("GROUNDING", prompt.SystemInstruction, StringComparison.Ordinal);
        Assert.Contains("ABSTAIN", prompt.SystemInstruction, StringComparison.Ordinal);
        Assert.Contains("CONTEXT", prompt.SystemInstruction, StringComparison.Ordinal);
    }

    [Fact]
    public void IncludesUnverifiedGuardLayer()
    {
        var prompt = Build();

        Assert.Contains("TẦNG 2", prompt.SystemInstruction, StringComparison.Ordinal);
        Assert.Contains("UNVERIFIED GUARD", prompt.SystemInstruction, StringComparison.Ordinal);
        Assert.Contains("(chưa đối chiếu bản in)", prompt.SystemInstruction, StringComparison.Ordinal);
        Assert.Contains("VERIFIED=False", prompt.UserPrompt, StringComparison.Ordinal);
    }

    [Fact]
    public void WrapsCorpusAsDataToResistPromptInjection()
    {
        var prompt = Build();

        Assert.Contains("TẦNG 3", prompt.SystemInstruction, StringComparison.Ordinal);
        Assert.Contains("CORPUS-AS-DATA", prompt.SystemInstruction, StringComparison.Ordinal);
        Assert.Contains("[BEGIN CORPUS DATA]", prompt.UserPrompt, StringComparison.Ordinal);
        Assert.Contains("[END CORPUS DATA]", prompt.UserPrompt, StringComparison.Ordinal);

        var begin = prompt.UserPrompt.IndexOf("[BEGIN CORPUS DATA]", StringComparison.Ordinal);
        var end = prompt.UserPrompt.IndexOf("[END CORPUS DATA]", StringComparison.Ordinal);
        var question = prompt.UserPrompt.IndexOf(Question, StringComparison.Ordinal);

        Assert.True(begin < end);
        Assert.True(question > end, "Câu hỏi phải nằm ngoài khối dữ liệu sách.");
    }

    [Fact]
    public void ContainsQuestionAndRetrievedChunkContent()
    {
        var prompt = Build();

        Assert.Contains(Question, prompt.UserPrompt, StringComparison.Ordinal);
        Assert.Contains(ChunkContent, prompt.UserPrompt, StringComparison.Ordinal);
        Assert.Contains("chương 4", prompt.UserPrompt, StringComparison.Ordinal);
        Assert.Contains("trang bản in 72", prompt.UserPrompt, StringComparison.Ordinal);
        Assert.Equal(StructuredResponseKind.GroundedAnswer, prompt.ResponseKind);
    }

    private static StructuredPrompt Build() =>
        new PromptBuilder().Build(Question, [RetrievedChunk()]);

    private static RetrievedChunk RetrievedChunk() => new()
    {
        Chunk = new KnowledgeChunk
        {
            CorpusId = Guid.NewGuid(),
            ChapterNumber = 4,
            ChapterTitle = "CHẾ ĐỘ ĂN TRƯỜNG THỌ",
            Content = ChunkContent,
            BookPageFrom = 72,
            BookPageTo = 72,
            PdfPageFrom = 64,
            PdfPageTo = 64,
            Verified = false,
            NeedsReview = true,
            SourceFile = "04-chuong-4-che-do-an-truong-tho.md",
        },
        Score = 0.9,
    };
}
