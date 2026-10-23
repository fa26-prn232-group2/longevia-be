using Longevia.Shared.Errors;
using Recommendation.Application.Abstractions;
using Recommendation.Application.Features.Search;
using Recommendation.Domain.Models;

namespace Recommendation.UnitTests.Features.Search;

/// <summary>
/// T086 - FR-020/FR-026: use case tra cứu giữ <c>top_k</c> trong khoảng 1..20
/// (mặc định 8), chuyển bộ lọc chương xuống kho tri thức, giữ thứ tự điểm giảm
/// dần và map đúng chương/trang/nội dung/điểm/trạng thái đối chiếu.
/// </summary>
public sealed class SearchLongevityKnowledgeUseCaseTests
{
    private const int DefaultTopK = 8;
    private const int MaxTopK = 20;

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public async Task UsesDefaultTopKWhenRequestedIsNotPositive(int requested)
    {
        var knowledge = new RecordingKnowledgeStore([Retrieved(0.9)]);

        await CreateUseCase(knowledge).ExecuteAsync(
            Request() with { TopK = requested },
            CancellationToken.None);

        var query = Assert.Single(knowledge.Queries);
        Assert.Equal(DefaultTopK, query.TopK);
    }

    [Theory]
    [InlineData(5, 5)]
    [InlineData(20, 20)]
    [InlineData(21, 20)]
    [InlineData(100, 20)]
    public async Task ClampsTopKToContractRange(int requested, int expected)
    {
        var knowledge = new RecordingKnowledgeStore([Retrieved(0.9)]);

        await CreateUseCase(knowledge).ExecuteAsync(
            Request() with { TopK = requested },
            CancellationToken.None);

        var query = Assert.Single(knowledge.Queries);
        Assert.Equal(expected, query.TopK);
        Assert.InRange(query.TopK, 1, MaxTopK);
    }

    [Fact]
    public async Task PassesChapterFilterToStore()
    {
        var knowledge = new RecordingKnowledgeStore([Retrieved(0.9, chapterNumber: 6)]);

        await CreateUseCase(knowledge).ExecuteAsync(
            Request() with { ChapterFilter = 6 },
            CancellationToken.None);

        var query = Assert.Single(knowledge.Queries);
        Assert.Equal(6, query.ChapterNumber);
    }

    [Fact]
    public async Task ReturnsSnippetsOrderedByScoreDescending()
    {
        var knowledge = new RecordingKnowledgeStore(
        [
            Retrieved(0.2, chapterNumber: 6),
            Retrieved(0.9, chapterNumber: 4),
            Retrieved(0.5, chapterNumber: 8),
        ]);

        var result = await CreateUseCase(knowledge).ExecuteAsync(Request(), CancellationToken.None);

        Assert.Equal(3, result.Snippets.Count);
        Assert.Equal(0.9, result.Snippets[0].Score);
        Assert.Equal(0.5, result.Snippets[1].Score);
        Assert.Equal(0.2, result.Snippets[2].Score);
    }

    [Fact]
    public async Task MapsChapterTitlePagesTextScoreAndVerified()
    {
        var knowledge = new RecordingKnowledgeStore([Retrieved(0.87, chapterNumber: 4)]);

        var result = await CreateUseCase(knowledge).ExecuteAsync(Request(), CancellationToken.None);

        var snippet = Assert.Single(result.Snippets);
        Assert.Equal(4, snippet.ChapterNumber);
        Assert.Equal("CHẾ ĐỘ ĂN TRƯỜNG THỌ", snippet.ChapterTitle);
        Assert.Equal(71, snippet.BookPage);
        Assert.Equal(63, snippet.PdfPage);
        Assert.Equal(ChunkContent, snippet.Text);
        Assert.Equal(0.87, snippet.Score);
        Assert.True(snippet.Verified);
    }

    [Fact]
    public async Task RejectsBlankQueryWithoutCallingStore()
    {
        var knowledge = new RecordingKnowledgeStore([Retrieved(0.9)]);

        var exception = await Assert.ThrowsAsync<AppException>(
            () => CreateUseCase(knowledge).ExecuteAsync(
                Request() with { Query = "   " },
                CancellationToken.None));

        Assert.Equal("recommendation.invalid_question", exception.Code);
        Assert.Empty(knowledge.Queries);
    }

    private const string ChunkContent = "Chất béo không bão hòa đơn có nhiều trong dầu ô liu.";

    private static SearchLongevityKnowledgeUseCase CreateUseCase(RecordingKnowledgeStore knowledge) =>
        new(knowledge);

    private static SearchKnowledgeRequest Request() => new()
    {
        Query = "nhịn ăn gián đoạn",
        ChapterFilter = 0,
        TopK = 0,
    };

    private static RetrievedChunk Retrieved(double score, int chapterNumber = 4) => new()
    {
        Chunk = new KnowledgeChunk
        {
            CorpusId = Guid.NewGuid(),
            ChapterNumber = chapterNumber,
            ChapterTitle = "CHẾ ĐỘ ĂN TRƯỜNG THỌ",
            Content = ChunkContent,
            BookPageFrom = 71,
            BookPageTo = 72,
            PdfPageFrom = 63,
            PdfPageTo = 64,
            Verified = true,
            NeedsReview = false,
            SourceFile = "04-chuong-4-che-do-an-truong-tho.md",
        },
        Score = score,
    };

    private sealed class RecordingKnowledgeStore(IReadOnlyList<RetrievedChunk> chunks) : IKnowledgeStore
    {
        public List<KnowledgeQuery> Queries { get; } = [];

        public Task<IReadOnlyList<RetrievedChunk>> SearchAsync(
            KnowledgeQuery query,
            CancellationToken cancellationToken)
        {
            Queries.Add(query);
            return Task.FromResult(chunks);
        }

        public Task<int> UpsertAsync(
            IReadOnlyList<KnowledgeChunk> chunks,
            CancellationToken cancellationToken) => Task.FromResult(0);

        public Task<int> CountAsync(string corpusKey, CancellationToken cancellationToken) =>
            Task.FromResult(chunks.Count);

        public Task<int> CountActiveAsync(CancellationToken cancellationToken) =>
            Task.FromResult(chunks.Count);
    }
}
