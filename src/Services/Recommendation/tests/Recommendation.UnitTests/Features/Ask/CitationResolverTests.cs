using System.Text.Json.Nodes;
using Recommendation.Application.Features.Ask;
using Recommendation.Domain.Models;

namespace Recommendation.UnitTests.Features.Ask;

/// <summary>
/// T078 — FR-017: chỉ giữ trích dẫn có <c>quote</c> xuất hiện nguyên văn trong
/// đoạn nguồn (sau khi gộp whitespace và bỏ dấu); mất hết trích dẫn thì abstain.
/// </summary>
public sealed class CitationResolverTests
{
    private const string FatContent =
        "Các chất béo không bão hòa có thể được chia thành chất béo không bão hòa đơn "
        + "(ví dụ như axit oleic trong dầu ô liu) hoặc chất béo không bão hòa đa "
        + "(chẳng hạn như trong cá hồi và dầu ngô).";

    private const string ProteinContent =
        "Protein cơ bản được cấu thành từ 20 axit amin, trình tự sắp xếp của chúng "
        + "xác định chức năng cụ thể của mỗi loại protein.";

    [Fact]
    public void KeepsCitationWhoseQuoteAppearsInChunk()
    {
        const string quote = "không bão hòa đơn (ví dụ như axit oleic trong dầu ô liu)";

        var resolution = Resolve(Citations(Citation(0, quote)));

        var citation = Assert.Single(resolution.Citations);
        Assert.False(resolution.Abstained);
        Assert.Equal(quote, citation.Quote);
        Assert.Equal(4, citation.ChapterNumber);
        Assert.Equal(72, citation.BookPage);
        Assert.Equal(64, citation.PdfPage);
        Assert.False(citation.Verified);
        Assert.Equal("04-chuong-4-che-do-an-truong-tho.md", citation.SourceFile);
    }

    [Fact]
    public void DropsCitationWhoseQuoteIsNotInAnyChunk()
    {
        var resolution = Resolve(Citations(Citation(0, "Đây là câu không hề xuất hiện trong sách.")));

        Assert.Empty(resolution.Citations);
        Assert.True(resolution.Abstained);
    }

    [Fact]
    public void AbstainsWhenEveryCitationIsDropped()
    {
        var resolution = Resolve(Citations(
            Citation(0, "câu thứ nhất không có trong sách"),
            Citation(1, "câu thứ hai cũng không có trong sách")));

        Assert.Empty(resolution.Citations);
        Assert.True(resolution.Abstained);
    }

    [Fact]
    public void MatchesQuoteWithoutDiacriticsAgainstAccentedChunk()
    {
        const string quote = "chat beo khong bao hoa don (vi du nhu axit oleic trong dau o liu)";

        var resolution = Resolve(Citations(Citation(0, quote)));

        var citation = Assert.Single(resolution.Citations);
        Assert.Equal(72, citation.BookPage);
    }

    [Fact]
    public void MatchesAccentedQuoteAgainstUnaccentedChunk()
    {
        const string quote = "Protein cơ bản được cấu thành từ 20 axit amin";

        var resolution = Resolve(Citations(Citation(1, quote)), useUnaccentedProteinChunk: true);

        var citation = Assert.Single(resolution.Citations);
        Assert.Equal(70, citation.BookPage);
        Assert.True(citation.Verified);
    }

    [Fact]
    public void UsesChunkIndexToResolveCitationFromCorrectChunk()
    {
        const string quote = "trình tự sắp xếp của chúng xác định chức năng cụ thể";

        var resolution = Resolve(Citations(Citation(1, quote)));

        var citation = Assert.Single(resolution.Citations);
        Assert.Equal(70, citation.BookPage);
        Assert.Equal(62, citation.PdfPage);
    }

    private static CitationResolution Resolve(
        JsonArray citations,
        bool useUnaccentedProteinChunk = false) =>
        new CitationResolver().Resolve(
            new JsonObject { ["citations"] = citations },
            [FatChunk(), ProteinChunk(useUnaccentedProteinChunk)]);

    private static JsonArray Citations(params JsonObject[] citations)
    {
        var array = new JsonArray();
        foreach (var citation in citations)
        {
            array.Add(citation);
        }

        return array;
    }

    private static JsonObject Citation(int chunkIndex, string quote) => new()
    {
        ["chunk_index"] = chunkIndex,
        ["quote"] = quote,
    };

    private static RetrievedChunk FatChunk() => Chunk(
        chapterNumber: 4,
        content: FatContent,
        bookPage: 72,
        pdfPage: 64,
        verified: false);

    private static RetrievedChunk ProteinChunk(bool unaccented) => Chunk(
        chapterNumber: 4,
        content: unaccented ? Unaccent(ProteinContent) : ProteinContent,
        bookPage: 70,
        pdfPage: 62,
        verified: true);

    private static RetrievedChunk Chunk(
        int chapterNumber,
        string content,
        int bookPage,
        int pdfPage,
        bool verified) => new()
        {
            Chunk = new KnowledgeChunk
            {
                CorpusId = Guid.NewGuid(),
                ChapterNumber = chapterNumber,
                ChapterTitle = "CHẾ ĐỘ ĂN TRƯỜNG THỌ",
                Content = content,
                BookPageFrom = bookPage,
                BookPageTo = bookPage,
                PdfPageFrom = pdfPage,
                PdfPageTo = pdfPage,
                Verified = verified,
                NeedsReview = !verified,
                SourceFile = "04-chuong-4-che-do-an-truong-tho.md",
            },
            Score = 0.9,
        };

    private static string Unaccent(string text)
    {
        var decomposed = text.Normalize(System.Text.NormalizationForm.FormD);
        var builder = new System.Text.StringBuilder(decomposed.Length);

        foreach (var character in decomposed)
        {
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(character)
                != System.Globalization.UnicodeCategory.NonSpacingMark)
            {
                builder.Append(character);
            }
        }

        return builder.ToString().Replace('đ', 'd').Replace('Đ', 'D');
    }
}
