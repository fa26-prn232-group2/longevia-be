using Recommendation.Application.Features.MealPlans;
using Recommendation.Domain.Constants;
using Recommendation.Domain.Models;

namespace Recommendation.UnitTests.Features.Plans;

/// <summary>
/// T028 — FR-017/FR-023: suggestion của ngày chạm trần chỉ lấy từ chương 4 hoặc
/// chương 12 có <c>book_page_from &gt;= 238</c> (Phụ lục A), tối đa 3, không có
/// <c>food_id</c>; không có nguồn thì rỗng + <c>suggestion_source_missing</c>.
/// </summary>
public sealed class MealPlanSuggestionBuilderTests
{
    private readonly MealPlanSuggestionBuilder _builder = new();

    [Fact]
    public void BuildsAtMostThreeSuggestionsFromAllowedSources()
    {
        var result = _builder.Build(
            "2026-10-28",
            [
                Chunk(4, 72, "Ưu tiên rau xanh và nước lọc trong ngày."),
                Chunk(12, 238, "Uống trà thảo mộc không đường để thư giãn."),
                Chunk(12, 239, "Đi bộ nhẹ sau bữa tối giúp dễ ngủ."),
                Chunk(12, 240, "Giữ nhịp sinh hoạt đều đặn."),
            ]);

        Assert.Equal(PlanDefaults.MaxSuggestionsPerDay, result.Suggestions.Count);
        Assert.Empty(result.Warnings);
        Assert.All(result.Suggestions, suggestion => Assert.NotEmpty(suggestion.Text));
        Assert.All(result.Suggestions, suggestion => Assert.NotEmpty(suggestion.Citations));
    }

    [Fact]
    public void ExcludesChapterTwelvePagesBeforeAppendix()
    {
        var result = _builder.Build(
            "2026-10-28",
            [Chunk(12, 237, "Nội dung trước Phụ lục A.")]);

        Assert.Empty(result.Suggestions);
        Assert.Contains(result.Warnings, warning => warning.Code == PlanWarningCodes.SuggestionSourceMissing);
    }

    [Fact]
    public void ExcludesChaptersOtherThanFourAndTwelve()
    {
        var result = _builder.Build(
            "2026-10-28",
            [Chunk(5, 90, "Hướng dẫn tập luyện.")]);

        Assert.Empty(result.Suggestions);
        Assert.Contains(result.Warnings, warning => warning.Code == PlanWarningCodes.SuggestionSourceMissing);
        Assert.Equal("2026-10-28", result.Warnings[0].Scope);
    }

    [Fact]
    public void RejectsChapterFourChunkWithoutSuggestionTopic()
    {
        var result = _builder.Build(
            "2026-10-28",
            [Chunk(4, 72, "Chế độ ăn cần cân bằng calo và macro giữa các bữa.")]);

        Assert.Empty(result.Suggestions);
        var warning = Assert.Single(result.Warnings);
        Assert.Equal(PlanWarningCodes.SuggestionSourceMissing, warning.Code);
        Assert.Equal("2026-10-28", warning.Scope);
    }

    [Fact]
    public void KeepsAppendixChunkOnSuggestionTopic()
    {
        var result = _builder.Build(
            "2026-10-28",
            [Chunk(12, 238, "Uống trà thảo mộc không đường vào buổi tối để thư giãn và ngủ ngon.")]);

        var suggestion = Assert.Single(result.Suggestions);
        Assert.Contains("trà thảo mộc", suggestion.Text, StringComparison.Ordinal);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void SuggestionHasNoFoodIdentifier()
    {
        Assert.Null(typeof(PlanSuggestion).GetProperty("FoodId"));

        var result = _builder.Build("2026-10-28", [Chunk(4, 72, "Uống đủ nước.")]);

        var suggestion = Assert.Single(result.Suggestions);
        Assert.DoesNotContain("f-", suggestion.Text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CitationQuoteIsCopiedFromTheChunkAndKeepsVerifiedFlag()
    {
        const string content = "Uống nước lọc thay cho đồ uống có đường.";
        var result = _builder.Build("2026-10-28", [Chunk(4, 72, content, verified: true)]);

        var citation = Assert.Single(Assert.Single(result.Suggestions).Citations);
        Assert.Contains(citation.Quote, content, StringComparison.Ordinal);
        Assert.True(citation.Verified);
        Assert.Equal(4, citation.ChapterNumber);
        Assert.Equal(72, citation.BookPage);
    }

    private static RetrievedChunk Chunk(
        int chapter,
        int bookPage,
        string content,
        bool verified = true) => new()
        {
            Chunk = new KnowledgeChunk
            {
                CorpusId = Guid.NewGuid(),
                ChapterNumber = chapter,
                ChapterTitle = $"Chương {chapter}",
                Content = content,
                BookPageFrom = bookPage,
                BookPageTo = bookPage,
                PdfPageFrom = bookPage,
                PdfPageTo = bookPage,
                Verified = verified,
                NeedsReview = !verified,
                SourceFile = $"chapter-{chapter}.md",
            },
            Score = 0.9,
        };
}
