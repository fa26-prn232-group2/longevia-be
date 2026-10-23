using System.Text.Json.Nodes;
using Longevia.Shared.Errors;
using Recommendation.Application.Abstractions;
using Recommendation.Application.Features.Ask;
using Recommendation.Domain.Constants;
using Recommendation.Domain.Models;

namespace Recommendation.UnitTests.Features.Ask;

/// <summary>
/// T080 — FR-015..FR-019/FR-031/FR-032/FR-033: use case hỏi tri thức chỉ trả lời
/// khi có trích dẫn đã verify, abstain khi thiếu căn cứ hoặc provider lỗi, và
/// chặn câu hỏi về thuốc/chẩn đoán trước khi gọi provider.
/// </summary>
public sealed class AskLongevityQuestionUseCaseTests
{
    private const string ChunkContent =
        "Chất béo không bão hòa đơn có nhiều trong dầu ô liu, các loại hạt và quả bơ.";
    private const string Question = "Chất béo không bão hòa đơn có trong thực phẩm nào?";

    [Fact]
    public async Task AbstainsWithCorpusNotLoadedReasonWhenStoreIsEmpty()
    {
        var chat = new StubChatModel(Grounded("câu trả lời", (0, "Chất béo")));
        var knowledge = new StubKnowledgeStore([]);

        var result = await CreateUseCase(chat, knowledge).ExecuteAsync(Request(), CancellationToken.None);

        Assert.True(result.Abstained);
        Assert.Equal("corpus_not_loaded", result.AbstainReason);
        Assert.Empty(result.Citations);
        Assert.Empty(chat.Prompts);
        Assert.False(string.IsNullOrWhiteSpace(result.Disclaimer));
    }

    [Fact]
    public async Task DistinguishesNoMatchingChunkFromAnEmptyCorpus()
    {
        var chat = new StubChatModel(Grounded("câu trả lời", (0, "Chất béo")));
        var knowledge = new StubKnowledgeStore([], activeChunks: 188);

        var result = await CreateUseCase(chat, knowledge).ExecuteAsync(Request(), CancellationToken.None);

        Assert.True(result.Abstained);
        Assert.Equal("no_matching_chunk", result.AbstainReason);
        Assert.Empty(chat.Prompts);
    }

    [Fact]
    public async Task ReturnsWarningWithoutFabricatedContentWhenProviderFails()
    {
        var chat = new StubChatModel(new ChatCompletion
        {
            Abstained = true,
            AbstainReason = "provider_unavailable",
        });

        var result = await CreateUseCase(chat).ExecuteAsync(Request(), CancellationToken.None);

        Assert.True(result.Abstained);
        Assert.Equal("provider_unavailable", result.AbstainReason);
        Assert.Empty(result.Citations);
        Assert.DoesNotContain(ChunkContent, result.Answer, StringComparison.Ordinal);
        Assert.False(string.IsNullOrWhiteSpace(result.Answer));
        Assert.Equal(DisclaimerText.Value, result.Disclaimer);
    }

    [Fact]
    public async Task AbstainsWhenModelReturnsAnswerWithoutVerifiedCitation()
    {
        var chat = new StubChatModel(Grounded("Chất béo không bão hòa đơn có trong dầu ô liu.", (0, "câu bịa")));

        var result = await CreateUseCase(chat).ExecuteAsync(Request(), CancellationToken.None);

        Assert.True(result.Abstained);
        Assert.Empty(result.Citations);
        Assert.False(string.IsNullOrWhiteSpace(result.AbstainReason));
    }

    [Fact]
    public async Task ReturnsGroundedAnswerWithVerifiedCitation()
    {
        var chat = new StubChatModel(Grounded(
            "Chất béo không bão hòa đơn có trong dầu ô liu.",
            (0, "Chất béo không bão hòa đơn có nhiều trong dầu ô liu")));

        var result = await CreateUseCase(chat).ExecuteAsync(Request(), CancellationToken.None);

        Assert.False(result.Abstained);
        Assert.Null(result.AbstainReason);
        var citation = Assert.Single(result.Citations);
        Assert.Equal(72, citation.BookPage);
        Assert.Equal(64, citation.PdfPage);
        Assert.Equal(4, citation.ChapterNumber);
        Assert.False(citation.Verified);
        Assert.Equal(DisclaimerText.Value, result.Disclaimer);
    }

    [Fact]
    public async Task RefusesMedicationQuestionBeforeProviderCall()
    {
        var chat = new StubChatModel(Grounded("Liều dùng nên là 2 viên mỗi ngày.", (0, "Chất béo")));
        var knowledge = new StubKnowledgeStore([RetrievedChunk()]);
        var request = Request() with { Question = "Tôi nên uống thuốc huyết áp với liều bao nhiêu?" };

        var result = await CreateUseCase(chat, knowledge).ExecuteAsync(request, CancellationToken.None);

        Assert.Empty(chat.Prompts);
        Assert.Equal(0, knowledge.SearchCount);
        Assert.Empty(result.Citations);
        Assert.Contains("bác sĩ", result.Answer, StringComparison.OrdinalIgnoreCase);
        Assert.False(string.IsNullOrWhiteSpace(result.Disclaimer));
    }

    [Fact]
    public async Task AnswersChronicDiseaseDietQuestionAtLifestyleLevel()
    {
        var chat = new StubChatModel(Grounded(
            "Người bị tiểu đường nên ưu tiên rau và ngũ cốc nguyên cám.",
            (0, "Chất béo không bão hòa đơn có nhiều trong dầu ô liu")));
        var request = Request() with { Question = "Người bị bệnh tiểu đường nên ăn gì?" };

        var result = await CreateUseCase(chat).ExecuteAsync(request, CancellationToken.None);

        Assert.Single(chat.Prompts);
        Assert.False(result.Abstained);
        Assert.NotEmpty(result.Citations);
        Assert.Equal(DisclaimerText.Value, result.Disclaimer);
    }

    [Fact]
    public async Task RejectsBlankQuestion()
    {
        var chat = new StubChatModel(Grounded("bất kỳ", (0, "Chất béo")));

        var exception = await Assert.ThrowsAsync<AppException>(
            () => CreateUseCase(chat).ExecuteAsync(Request() with { Question = "   " }, CancellationToken.None));

        Assert.Equal("recommendation.invalid_question", exception.Code);
    }

    private static AskLongevityQuestionUseCase CreateUseCase(
        StubChatModel chat,
        StubKnowledgeStore? knowledge = null) =>
        new(
            knowledge ?? new StubKnowledgeStore([RetrievedChunk()]),
            new PromptBuilder(),
            chat,
            new CitationResolver(),
            new SafetyGuard());

    private static AskLongevityQuestionRequest Request() => new()
    {
        UserId = "user-1",
        Question = Question,
        ChapterFilter = 0,
    };

    private static ChatCompletion Grounded(string answer, params (int ChunkIndex, string Quote)[] citations)
    {
        var array = new JsonArray();
        foreach (var (chunkIndex, quote) in citations)
        {
            array.Add(new JsonObject
            {
                ["chunk_index"] = chunkIndex,
                ["quote"] = quote,
            });
        }

        return new ChatCompletion
        {
            Payload = new JsonObject
            {
                ["answer"] = answer,
                ["citations"] = array,
            },
        };
    }

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

    private sealed class StubChatModel(ChatCompletion completion) : IChatModel
    {
        public List<StructuredPrompt> Prompts { get; } = [];

        public Task<ChatCompletion> CompleteAsync(StructuredPrompt prompt, CancellationToken cancellationToken)
        {
            Prompts.Add(prompt);
            return Task.FromResult(completion);
        }
    }

    private sealed class StubKnowledgeStore(IReadOnlyList<RetrievedChunk> chunks, int? activeChunks = null) : IKnowledgeStore
    {
        public int SearchCount { get; private set; }

        public Task<IReadOnlyList<RetrievedChunk>> SearchAsync(
            KnowledgeQuery query,
            CancellationToken cancellationToken)
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
