using System.Text.Json.Nodes;
using Recommendation.Application.Abstractions;
using Recommendation.Application.Features.Ask;
using Recommendation.Application.Features.Meals;
using Recommendation.Application.Features.NutrientGaps;
using Recommendation.Domain.Constants;
using Recommendation.Domain.Models;

namespace Recommendation.UnitTests.Features.NutrientGaps;

/// <summary>
/// T093 — FR-009/FR-011: cùng đầu vào cho cùng kết quả, thực phẩm gợi ý không
/// chứa thành phần dị ứng, phần diễn giải chỉ có trích dẫn khi corpus có nguồn.
/// </summary>
public sealed class AnalyzeNutrientGapsUseCaseTests
{
    private const string ChunkContent =
        "Chất xơ có nhiều trong yến mạch, đậu lăng và bông cải xanh. "
        + "Protein có trong trứng, sữa chua, cá hồi và đậu hũ.";

    private const string CitationQuote = "Chất xơ có nhiều trong yến mạch";

    private static readonly UserProfile Profile = new()
    {
        Age = 30,
        Sex = "FEMALE",
        Allergies = ["trứng", "sữa"],
    };

    [Fact]
    public async Task ComputesProteinAndFiberGapsFromTheSevenDayAverage()
    {
        var result = await CreateUseCase().ExecuteAsync(Request(), CancellationToken.None);

        Assert.Equal(["Protein", "Fiber"], result.Gaps.Select(gap => gap.Nutrient));
        Assert.Equal(["Protein", "Fiber"], result.DeficientNutrients);

        var protein = result.Gaps[0];
        Assert.Equal(60, protein.Target, 3);
        Assert.Equal(20, protein.AverageIntake, 3);
        Assert.Equal(40, protein.Deficit, 3);
        Assert.Equal("severe", protein.Severity);

        var fiber = result.Gaps[1];
        Assert.Equal(25, fiber.Target, 3);
        Assert.Equal(15, fiber.Deficit, 3);
        Assert.Equal("severe", fiber.Severity);
    }

    [Fact]
    public async Task ProducesTheSameResultForTheSameInput()
    {
        var useCase = CreateUseCase();

        var first = await useCase.ExecuteAsync(Request(), CancellationToken.None);
        var second = await useCase.ExecuteAsync(Request(), CancellationToken.None);

        Assert.Equal(first.Gaps, second.Gaps);
        Assert.Equal(first.DeficientNutrients, second.DeficientNutrients);
        Assert.Equal(first.SuggestedFoods, second.SuggestedFoods);
        Assert.Equal(first.AnalysisSummary, second.AnalysisSummary);
    }

    [Fact]
    public async Task FiltersSuggestedFoodsThatContainDeclaredAllergens()
    {
        var result = await CreateUseCase().ExecuteAsync(Request(), CancellationToken.None);

        Assert.NotEmpty(result.SuggestedFoods);
        Assert.DoesNotContain(
            result.SuggestedFoods,
            food => AllergenAliases.ContainsPhrase(food, "trứng"));
        Assert.DoesNotContain(
            result.SuggestedFoods,
            food => AllergenAliases.ContainsPhrase(food, "sữa"));
    }

    [Fact]
    public async Task ReturnsVerifiedCitationsWhenTheCorpusHasSources()
    {
        var result = await CreateUseCase().ExecuteAsync(Request(), CancellationToken.None);

        var citation = Assert.Single(result.Citations);
        Assert.Equal(CitationQuote, citation.Quote);
        Assert.Equal(4, citation.ChapterNumber);
        Assert.Equal(72, citation.BookPage);
        Assert.False(string.IsNullOrWhiteSpace(result.AnalysisSummary));
        Assert.NotEqual(DisclaimerText.Value, result.AnalysisSummary);
    }

    [Fact]
    public async Task ReturnsNoCitationAndNoModelCallWhenTheCorpusIsEmpty()
    {
        var chat = new StubChatModel(Payload());
        var knowledge = new StubKnowledgeStore([]);

        var result = await CreateUseCase(chat, knowledge)
            .ExecuteAsync(Request(), CancellationToken.None);

        Assert.Empty(result.Citations);
        Assert.Empty(result.SuggestedFoods);
        Assert.False(string.IsNullOrWhiteSpace(result.AnalysisSummary));
        Assert.Empty(chat.Prompts);
    }

    [Fact]
    public async Task AlwaysIncludesTheMandatoryDisclaimer()
    {
        var result = await CreateUseCase().ExecuteAsync(Request(), CancellationToken.None);

        Assert.Equal(DisclaimerText.Value, result.Disclaimer);
    }

    private static AnalyzeNutrientGapsUseCase CreateUseCase(
        StubChatModel? chat = null,
        StubKnowledgeStore? knowledge = null) =>
        new(
            new NutrientGapCalculator(),
            knowledge ?? new StubKnowledgeStore([RetrievedChunk()]),
            new PromptBuilder(),
            chat ?? new StubChatModel(Payload()),
            new CitationResolver(),
            new AllergenGuard());

    private static NutrientGapRequest Request() => new()
    {
        UserId = "user-1",
        Profile = Profile,
        Past7Days = Enumerable.Range(0, 7).Select(_ => Day()).ToList(),
        Targets = new NutrientTargets
        {
            TargetCalories = 2000,
            TargetProteinGrams = 60,
            TargetCarbsGrams = 130,
            TargetFatGrams = 67,
            TargetFiberGrams = 25,
        },
    };

    private static DailyNutrientSummary Day() => new()
    {
        Date = "2026-10-22",
        Calories = 2000,
        Protein = 20,
        Carbs = 130,
        Fat = 67,
        Fiber = 10,
    };

    private static ChatCompletion Payload()
    {
        var citations = new JsonArray(
            new JsonObject
            {
                ["chunk_index"] = 0,
                ["quote"] = CitationQuote,
            });

        return new ChatCompletion
        {
            Payload = new JsonObject
            {
                ["analysis_summary"] = "Theo sách, nên bổ sung chất xơ từ yến mạch và protein từ cá hồi.",
                ["suggested_foods"] = new JsonArray(
                    "Yến mạch",
                    "Đậu lăng",
                    "Trứng",
                    "Sữa chua",
                    "Cá hồi",
                    "Đậu hũ"),
                ["citations"] = citations,
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

        public Task<int> CountActiveAsync(CancellationToken cancellationToken) =>
            Task.FromResult(chunks.Count);
    }
}
