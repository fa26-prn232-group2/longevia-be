using Longevia.Recommendation.Contracts.V1;
using Recommendation.IntegrationTests.Features.Meals;
using Recommendation.IntegrationTests.Features.Shared;

namespace Recommendation.IntegrationTests.Features.Search;

/// <summary>
/// T087 - FR-020/FR-026 + US4: host thật + PostgreSQL thật, nạp các đoạn corpus
/// thật của chương 4 rồi tra cứu qua gRPC bằng tiếng Việt có dấu. Kết quả phải
/// kèm chương, trang bản in, trang scan và điểm; lọc theo chương chỉ trả đoạn
/// thuộc chương đó; <c>top_k</c> lớn hơn số đoạn có phải trả hết, không lỗi.
/// </summary>
public sealed class SearchLongevityKnowledgeGrpcTests(RecommendationApiFixture fixture)
    : IClassFixture<RecommendationApiFixture>, IAsyncLifetime
{
    private const string Query = "chất béo không bão hòa đơn";
    private const int BookPageFrom = 69;
    private const int BookPageTo = 105;
    private const int PdfPageFrom = 61;
    private const int PdfPageTo = 98;

    private static readonly SeedChunk ProteinChunk = new()
    {
        Content =
            "Protein cơ bản được cấu thành từ 20 axit amin, trình tự sắp xếp của chúng "
            + "xác định chức năng cụ thể của mỗi loại protein.",
        BookPageFrom = 70,
        PdfPageFrom = 62,
        Verified = true,
    };

    private static readonly SeedChunk FatChunk = new()
    {
        Content =
            "Chất béo có thể ở dạng bão hòa (khi mỗi nguyên tử carbon liên kết với số lượng "
            + "tối đa nguyên tử hydro) hoặc không bão hòa (khi carbon liên kết với ít hơn số "
            + "nguyên tử hydro tối đa). Các chất béo không bão hòa có thể được chia thành chất "
            + "béo không bão hòa đơn (ví dụ như axit oleic trong dầu ô liu) hoặc chất béo không "
            + "bão hòa đa (chẳng hạn như trong cá hồi và dầu ngô).",
        BookPageFrom = 72,
        PdfPageFrom = 64,
        Verified = false,
        NeedsReview = true,
    };

    private static readonly SeedChunk MicronutrientChunk = new()
    {
        Content =
            "Trong chương này, tôi sẽ mô tả ngắn gọn các thành phần này và tác dụng của chúng. "
            + "Sau đó, tôi sẽ giải thích vai trò của chúng trong quá trình lão hóa và bệnh tật, "
            + "dựa trên bằng chứng sử dụng Năm Trụ cột của sự Trường thọ.",
        BookPageFrom = 70,
        PdfPageFrom = 62,
        Verified = true,
    };

    public Task InitializeAsync() =>
        CorpusSeeder.SeedAsync(
            fixture.Services,
            [ProteinChunk, FatChunk, MicronutrientChunk],
            CancellationToken.None);

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task ReturnsRankedSnippetsWithChapterAndPagesForVietnameseQuery()
    {
        var response = await fixture.Client.SearchLongevityKnowledgeAsync(new SearchKnowledgeRequest
        {
            Query = Query,
            ChapterFilter = 0,
            TopK = 8,
        });

        Assert.NotEmpty(response.Snippets);
        AssertDescendingScore(response.Snippets);

        var best = response.Snippets[0];
        Assert.Contains("không bão hòa đơn", best.Text, StringComparison.Ordinal);
        Assert.Equal(CorpusSeeder.ChapterNumber, best.ChapterNumber);
        Assert.Equal(CorpusSeeder.ChapterTitle, best.ChapterTitle);
        Assert.Equal(FatChunk.BookPageFrom, best.BookPage);
        Assert.Equal(FatChunk.PdfPageFrom, best.PdfPage);
        Assert.False(best.Verified);
        Assert.True(best.Score > 0);
    }

    [Fact]
    public async Task ReturnsAllAvailableChunksWhenTopKExceedsCorpusSize()
    {
        var response = await fixture.Client.SearchLongevityKnowledgeAsync(new SearchKnowledgeRequest
        {
            Query = Query,
            ChapterFilter = 0,
            TopK = 20,
        });

        Assert.Equal(3, response.Snippets.Count);
        Assert.All(
            response.Snippets,
            snippet =>
            {
                Assert.InRange(snippet.BookPage, BookPageFrom, BookPageTo);
                Assert.InRange(snippet.PdfPage, PdfPageFrom, PdfPageTo);
                Assert.Equal(CorpusSeeder.ChapterTitle, snippet.ChapterTitle);
            });
    }

    [Fact]
    public async Task ChapterFilterReturnsOnlyChunksOfRequestedChapter()
    {
        var matching = await fixture.Client.SearchLongevityKnowledgeAsync(new SearchKnowledgeRequest
        {
            Query = Query,
            ChapterFilter = CorpusSeeder.ChapterNumber,
            TopK = 20,
        });

        Assert.Equal(3, matching.Snippets.Count);
        Assert.All(
            matching.Snippets,
            snippet => Assert.Equal(CorpusSeeder.ChapterNumber, snippet.ChapterNumber));

        var otherChapter = await fixture.Client.SearchLongevityKnowledgeAsync(new SearchKnowledgeRequest
        {
            Query = Query,
            ChapterFilter = 5,
            TopK = 8,
        });

        Assert.Empty(otherChapter.Snippets);
    }

    private static void AssertDescendingScore(IReadOnlyList<KnowledgeSnippet> snippets)
    {
        for (var index = 1; index < snippets.Count; index++)
        {
            Assert.True(
                snippets[index - 1].Score >= snippets[index].Score,
                "Điểm liên quan phải sắp xếp giảm dần.");
        }
    }
}
