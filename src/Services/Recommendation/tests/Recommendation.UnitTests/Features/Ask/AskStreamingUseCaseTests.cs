using Recommendation.Application.Abstractions;
using Recommendation.Application.Features.Ask;
using Recommendation.Domain.Models;

namespace Recommendation.UnitTests.Features.Ask;

public sealed class AskStreamingUseCaseTests
{
    [Fact]
    public async Task Greeting_AnsweredWithoutCallingModelOrStore()
    {
        var store = new StubKnowledgeStore([Chunk(1, 10)]);
        var model = new StubStreamModel(["không được gọi"]);
        var useCase = Create(store, model);

        var events = await Collect(useCase.ExecuteAsync(
            new AskStreamRequest("Chào bạn", 0, []), CancellationToken.None));

        Assert.IsType<AskDeltaEvent>(events[0]);
        var done = Assert.IsType<AskDoneEvent>(events[^1]);
        Assert.Equal("meta_intent", done.AbstainReason);
        Assert.Equal(0, store.SearchCount);
        Assert.Equal(0, model.CallCount);
    }

    [Fact]
    public async Task MedicalQuestion_RefusedWithoutCallingModel()
    {
        var store = new StubKnowledgeStore([Chunk(1, 10)]);
        var model = new StubStreamModel(["không được gọi"]);
        var useCase = Create(store, model);

        var events = await Collect(useCase.ExecuteAsync(
            new AskStreamRequest("Uống thuốc gì để chữa bệnh tiểu đường?", 0, []),
            CancellationToken.None));

        var done = Assert.IsType<AskDoneEvent>(events[^1]);
        Assert.Equal("medical_refusal", done.AbstainReason);
        Assert.Equal(0, model.CallCount);
    }

    [Fact]
    public async Task NoChunks_AbstainsWithNoMatchingChunkAndStreamsFriendlyAnswer()
    {
        var store = new StubKnowledgeStore([], activeChunks: 5);
        var model = new StubStreamModel(["không được gọi"]);
        var useCase = Create(store, model);

        var events = await Collect(useCase.ExecuteAsync(
            new AskStreamRequest("Nhịn ăn 16:8 là gì?", 0, []), CancellationToken.None));

        Assert.Contains(events, item => item is AskDeltaEvent);
        var done = Assert.IsType<AskDoneEvent>(events[^1]);
        Assert.True(done.Abstained);
        Assert.Equal("no_matching_chunk", done.AbstainReason);
        Assert.Equal(0, model.CallCount);
    }

    [Fact]
    public async Task StreamsDeltasThenCitationsThenDone()
    {
        var store = new StubKnowledgeStore([Chunk(2, 30), Chunk(2, 31)]);
        var model = new StubStreamModel(["Uống ", "đủ nước", " mỗi ngày."]);
        var useCase = Create(store, model);

        var events = await Collect(useCase.ExecuteAsync(
            new AskStreamRequest("Uống nước thế nào?", 0, []), CancellationToken.None));

        Assert.Equal(3, events.OfType<AskDeltaEvent>().Count());
        Assert.Equal(2, events.OfType<AskCitationEvent>().Count());
        Assert.Equal(1, model.CallCount);

        var done = Assert.IsType<AskDoneEvent>(events[^1]);
        Assert.False(done.Abstained);
        Assert.False(string.IsNullOrWhiteSpace(done.Disclaimer));
        Assert.IsType<AskCitationEvent>(events[^2]);
    }

    [Fact]
    public async Task ShortFollowUp_UsesPreviousUserTurnForRetrieval()
    {
        var store = new StubKnowledgeStore([Chunk(3, 40)]);
        var model = new StubStreamModel(["ok"]);
        var useCase = Create(store, model);
        var history = new List<ConversationTurn>
        {
            new("user", "Nhịn ăn gián đoạn 16:8 có lợi ích gì?"),
            new("assistant", "Giúp ổn định insulin."),
        };

        await Collect(useCase.ExecuteAsync(
            new AskStreamRequest("còn 18:6 thì sao?", 0, history), CancellationToken.None));

        Assert.Contains("16:8", model.LastUserPrompt, StringComparison.Ordinal);
        Assert.Contains("18:6", model.LastUserPrompt, StringComparison.Ordinal);
    }

    private static AskStreamingUseCase Create(IKnowledgeStore store, IChatStreamModel model) =>
        new(store, new PromptBuilder(), model, new SafetyGuard());

    private static async Task<List<AskStreamEvent>> Collect(IAsyncEnumerable<AskStreamEvent> stream)
    {
        var events = new List<AskStreamEvent>();
        await foreach (var item in stream)
        {
            events.Add(item);
        }

        return events;
    }

    private static RetrievedChunk Chunk(int chapter, int page) => new()
    {
        Chunk = new KnowledgeChunk
        {
            CorpusId = Guid.NewGuid(),
            Content = "Uống đủ nước giúp duy trì năng lượng và hỗ trợ tiêu hóa trong ngày.",
            ChapterNumber = chapter,
            ChapterTitle = $"Chương {chapter}",
            BookPageFrom = page,
            BookPageTo = page,
            PdfPageFrom = page,
            PdfPageTo = page,
            Verified = true,
            NeedsReview = false,
            SourceFile = "sach.md",
        },
        Score = 0.9,
    };

    private sealed class StubStreamModel(IReadOnlyList<string> chunks) : IChatStreamModel
    {
        public int CallCount { get; private set; }
        public string LastUserPrompt { get; private set; } = string.Empty;

        public async IAsyncEnumerable<string> StreamTextAsync(string systemInstruction, string userPrompt,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            CallCount++;
            LastUserPrompt = userPrompt;
            foreach (var chunk in chunks)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return chunk;
                await Task.Yield();
            }
        }
    }

    private sealed class StubKnowledgeStore(IReadOnlyList<RetrievedChunk> chunks, int? activeChunks = null)
        : IKnowledgeStore
    {
        public int SearchCount { get; private set; }

        public Task<IReadOnlyList<RetrievedChunk>> SearchAsync(KnowledgeQuery query, CancellationToken cancellationToken)
        {
            SearchCount++;
            return Task.FromResult(chunks);
        }

        public Task<int> UpsertAsync(IReadOnlyList<KnowledgeChunk> chunks, CancellationToken cancellationToken) =>
            Task.FromResult(0);

        public Task<int> CountAsync(string corpusKey, CancellationToken cancellationToken) =>
            Task.FromResult(chunks.Count);

        public Task<int> CountActiveAsync(CancellationToken cancellationToken) =>
            Task.FromResult(activeChunks ?? chunks.Count);
    }
}
