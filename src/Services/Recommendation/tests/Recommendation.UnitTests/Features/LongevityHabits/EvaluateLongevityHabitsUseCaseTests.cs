using System.Text.Json.Nodes;
using Recommendation.Application.Abstractions;
using Recommendation.Application.Features.Ask;
using Recommendation.Application.Features.LongevityHabits;
using Recommendation.Domain.Constants;
using Recommendation.Domain.Models;

namespace Recommendation.UnitTests.Features.LongevityHabits;

/// <summary>
/// T101 — FR-012..FR-014: score_breakdown luôn có đủ bốn nhánh với trọng số cộng
/// lại bằng 100, điểm tổng nằm trong 0..100, actionable_insights chỉ có trích dẫn
/// khi corpus có nguồn, và mọi phản hồi đều kèm disclaimer.
/// </summary>
public sealed class EvaluateLongevityHabitsUseCaseTests
{
    private const string ChunkContent =
        "Nhịn ăn gián đoạn 16 giờ giúp cơ thể nghỉ ngơi và tái tạo tế bào. "
        + "Giấc ngủ đủ 7 giờ hỗ trợ phục hồi, còn uống đủ nước giúp duy trì trao đổi chất.";

    private const string CitationQuote = "Nhịn ăn gián đoạn 16 giờ";

    [Fact]
    public async Task ReturnsFourWeightedComponentsAndAScoreWithinZeroToHundred()
    {
        var result = await CreateUseCase().ExecuteAsync(Request(), CancellationToken.None);

        Assert.Equal(
            [
                LongevityRubric.FastingKey,
                LongevityRubric.SleepKey,
                LongevityRubric.HydrationKey,
                LongevityRubric.NutritionKey,
            ],
            result.Components.Select(component => component.Key));
        Assert.Equal(100, result.Components.Sum(component => component.Weight));
        Assert.InRange(result.LongevityScore, 0, 100);
        Assert.All(result.Components, component => Assert.False(string.IsNullOrWhiteSpace(component.Reason)));
    }

    [Fact]
    public async Task ReturnsGroundedInsightsAndVerifiedCitationsWhenTheCorpusHasSources()
    {
        var result = await CreateUseCase().ExecuteAsync(Request(), CancellationToken.None);

        Assert.Equal(2, result.ActionableInsights.Count);
        Assert.Contains("Duy trì nhịn ăn 16 giờ mỗi ngày.", result.ActionableInsights);
        Assert.Contains("Ngủ đủ 7 giờ mỗi đêm.", result.ActionableInsights);

        var citation = Assert.Single(result.Citations);
        Assert.Equal(CitationQuote, citation.Quote);
        Assert.Equal(4, citation.ChapterNumber);
        Assert.Equal(72, citation.BookPage);
        Assert.True(citation.Verified);
    }

    [Fact]
    public async Task ReturnsStaticTipsWithoutCitationsAndSkipsTheModelWhenTheCorpusIsEmpty()
    {
        var chat = new StubChatModel(Payload());
        var knowledge = new StubKnowledgeStore([]);

        var result = await CreateUseCase(chat, knowledge)
            .ExecuteAsync(Request(), CancellationToken.None);

        Assert.Empty(result.Citations);
        Assert.Empty(chat.Prompts);
        Assert.NotEmpty(result.ActionableInsights);
        Assert.Equal(LongevityTips.Hydration, result.ActionableInsights[0]);
        Assert.All(
            result.ActionableInsights,
            insight => Assert.Contains(
                insight,
                new[]
                {
                    LongevityTips.Fasting,
                    LongevityTips.Sleep,
                    LongevityTips.Hydration,
                    LongevityTips.Nutrition,
                }));
    }

    [Fact]
    public async Task FallsBackToStaticTipsWhenTheModelAbstains()
    {
        var chat = new StubChatModel(new ChatCompletion { Abstained = true });

        var result = await CreateUseCase(chat).ExecuteAsync(Request(), CancellationToken.None);

        Assert.Empty(result.Citations);
        Assert.NotEmpty(result.ActionableInsights);
        Assert.Equal(DisclaimerText.Value, result.Disclaimer);
    }

    [Fact]
    public async Task AlwaysIncludesTheMandatoryDisclaimer()
    {
        var result = await CreateUseCase().ExecuteAsync(Request(), CancellationToken.None);

        Assert.Equal(DisclaimerText.Value, result.Disclaimer);
    }

    [Fact]
    public async Task ProducesTheSameResultForTheSameInput()
    {
        var useCase = CreateUseCase();

        var first = await useCase.ExecuteAsync(Request(), CancellationToken.None);
        var second = await useCase.ExecuteAsync(Request(), CancellationToken.None);

        Assert.Equal(first.LongevityScore, second.LongevityScore);
        Assert.Equal(first.Components, second.Components);
        Assert.Equal(first.ActionableInsights, second.ActionableInsights);
        Assert.Equal(first.Citations, second.Citations);
        Assert.Equal(first.Disclaimer, second.Disclaimer);
    }

    private static EvaluateLongevityHabitsUseCase CreateUseCase(
        StubChatModel? chat = null,
        StubKnowledgeStore? knowledge = null) =>
        new(
            new LongevityRubricCalculator(),
            knowledge ?? new StubKnowledgeStore([RetrievedChunk()]),
            new PromptBuilder(),
            chat ?? new StubChatModel(Payload()),
            new CitationResolver());

    private static LongevityHabitRequest Request() => new()
    {
        UserId = "user-1",
        Profile = new UserProfile { Age = 30, Sex = "FEMALE" },
        AverageFastingHours = 10d,
        AverageSleepHours = 4d,
        WaterTargetHitDays = 2,
        NutritionAdherenceRate = 40d,
    };

    private static ChatCompletion Payload() => new()
    {
        Payload = new JsonObject
        {
            ["answer"] = "Duy trì nhịn ăn 16 giờ mỗi ngày.\nNgủ đủ 7 giờ mỗi đêm.",
            ["citations"] = new JsonArray(
                new JsonObject
                {
                    ["chunk_index"] = 0,
                    ["quote"] = CitationQuote,
                }),
        },
    };

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
            Verified = true,
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

    private sealed class StubKnowledgeStore(IReadOnlyList<RetrievedChunk> chunks) : IKnowledgeStore
    {
        public Task<IReadOnlyList<RetrievedChunk>> SearchAsync(
            KnowledgeQuery query,
            CancellationToken cancellationToken) =>
            Task.FromResult(chunks);

        public Task<int> UpsertAsync(IReadOnlyList<KnowledgeChunk> chunks, CancellationToken cancellationToken) =>
            Task.FromResult(0);

        public Task<int> CountAsync(string corpusKey, CancellationToken cancellationToken) =>
            Task.FromResult(chunks.Count);
    }
}
