using System.Text.Json.Nodes;
using Longevia.Shared.Errors;
using Recommendation.Application.Abstractions;
using Recommendation.Application.Features.Ask;
using Recommendation.Application.Features.MealPlans;
using Recommendation.Application.Features.Meals;
using Recommendation.Application.Features.Plans;
using Recommendation.Domain.Constants;
using Recommendation.Domain.Models;

namespace Recommendation.UnitTests.Features.MealPlans;

/// <summary>
/// T030 — FR-013..FR-025: happy path, provider abstain/throw → fallback, corpus
/// rỗng, lỗi đầu vào, rule 105%, metadata và safety của <c>GenerateMealPlan</c>.
/// </summary>
public sealed class GenerateMealPlanUseCaseTests
{
    private const string WeekStart = "2026-10-26";
    private const string CappedDate = "2026-10-28";

    [Fact]
    public async Task ReturnsCompletePlanOnHappyPath()
    {
        var chat = new StubChatModel(SelectionFor(7));
        var useCase = CreateUseCase(chat, new StubKnowledgeStore());

        var result = await useCase.ExecuteAsync(Request(), CancellationToken.None);

        Assert.Equal(7, result.Days.Count);
        Assert.Equal(WeekStart, result.WeekStartDate);
        Assert.Equal(7, result.DaysRequested);
        Assert.Equal(14_000, result.WeeklyTargetCalories);
        Assert.Equal(700, result.WeeklyTargetProteinGrams, 3);
        Assert.Equal(1_750, result.WeeklyTargetCarbsGrams, 3);
        Assert.Equal(469, result.WeeklyTargetFatGrams, 3);
        Assert.False(result.UsedFallback);
        Assert.All(result.Days, day => Assert.Single(day.Meals));
        Assert.False(string.IsNullOrWhiteSpace(result.Disclaimer));
    }

    [Fact]
    public async Task AddsFiberBelowTargetWarningForEachDay()
    {
        var useCase = CreateUseCase(new StubChatModel(SelectionFor(7)), new StubKnowledgeStore());

        var result = await useCase.ExecuteAsync(Request(), CancellationToken.None);

        var fiberWarnings = result.Warnings
            .Where(warning => warning.Code == PlanWarningCodes.FiberBelowTarget)
            .ToList();

        Assert.Equal(7, fiberWarnings.Count);
        Assert.All(fiberWarnings, warning => Assert.NotEqual(PlanWarning.PlanScope, warning.Scope));
        Assert.All(
            fiberWarnings,
            warning => Assert.Contains("chất xơ", warning.Message, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task DoesNotAddFiberWarningWhenTargetIsNotProvided()
    {
        var useCase = CreateUseCase(new StubChatModel(SelectionFor(7)), new StubKnowledgeStore());

        var result = await useCase.ExecuteAsync(
            Request() with { TargetFiberGrams = 0 },
            CancellationToken.None);

        Assert.DoesNotContain(result.Warnings, warning => warning.Code == PlanWarningCodes.FiberBelowTarget);
    }

    [Fact]
    public async Task FallsBackWhenProviderAbstains()
    {
        var useCase = CreateUseCase(new StubChatModel(null), new StubKnowledgeStore());

        var result = await useCase.ExecuteAsync(Request(), CancellationToken.None);

        Assert.True(result.UsedFallback);
        Assert.Contains(result.Warnings, warning => warning.Code == PlanWarningCodes.AiFallbackUsed);
        Assert.Equal(7, result.Days.Count);
        Assert.All(result.Days, day => Assert.Single(day.Meals));
    }

    [Fact]
    public async Task FallsBackWhenProviderThrowsTransportException()
    {
        var useCase = CreateUseCase(new StubChatModel(throwOnCall: true), new StubKnowledgeStore());

        var result = await useCase.ExecuteAsync(Request(), CancellationToken.None);

        Assert.True(result.UsedFallback);
        Assert.Contains(result.Warnings, warning => warning.Code == PlanWarningCodes.AiFallbackUsed);
        Assert.Equal(7, result.Days.Count);
    }

    [Fact]
    public async Task ReturnsCorpusWarningWhenKnowledgeStoreIsEmpty()
    {
        var useCase = CreateUseCase(new StubChatModel(SelectionFor(7)), new StubKnowledgeStore());

        var result = await useCase.ExecuteAsync(Request(), CancellationToken.None);

        Assert.Empty(result.Citations);
        Assert.Contains(result.Warnings, warning => warning.Code == PlanWarningCodes.CorpusNotLoaded);
    }

    [Fact]
    public async Task MissingTargetsReturnsBusinessError()
    {
        var useCase = CreateUseCase(new StubChatModel(SelectionFor(7)), new StubKnowledgeStore());

        var exception = await Assert.ThrowsAsync<AppException>(
            () => useCase.ExecuteAsync(Request() with { TargetCalories = 0 }, CancellationToken.None));

        Assert.Equal("recommendation.missing_targets", exception.Code);
    }

    [Fact]
    public async Task NegativeFiberReturnsInvalidTargets()
    {
        var useCase = CreateUseCase(new StubChatModel(SelectionFor(7)), new StubKnowledgeStore());

        var exception = await Assert.ThrowsAsync<AppException>(
            () => useCase.ExecuteAsync(Request() with { TargetFiberGrams = -1 }, CancellationToken.None));

        Assert.Equal("recommendation.invalid_targets", exception.Code);
    }

    [Fact]
    public async Task AllCandidatesExcludedReturnsBusinessError()
    {
        var useCase = CreateUseCase(new StubChatModel(SelectionFor(7)), new StubKnowledgeStore());
        var request = Request() with
        {
            Profile = Profile() with { Allergies = ["đậu phộng"] },
            CandidateFoods = [Food("f-peanut", calories: 200, allergens: ["đậu phộng"])],
        };

        var exception = await Assert.ThrowsAsync<AppException>(
            () => useCase.ExecuteAsync(request, CancellationToken.None));

        Assert.Equal("recommendation.all_candidates_excluded", exception.Code);
    }

    [Fact]
    public async Task CapsDayAtOneHundredFivePercent()
    {
        var chunks = new StubKnowledgeStore([Chunk(4, 72, "Uống nước lọc và nghỉ ngơi hợp lý.")]);
        var useCase = CreateUseCase(new StubChatModel(SelectionFor(7)), chunks);
        var request = Request() with { ConsumedCalories = 2100, ConsumedDate = CappedDate };

        var result = await useCase.ExecuteAsync(request, CancellationToken.None);

        var capped = Assert.Single(result.Days, day => day.CalorieCapReached);
        Assert.Equal(CappedDate, capped.Date);
        Assert.Empty(capped.Meals);
        Assert.Equal(0, capped.TotalCalories);
        Assert.InRange(capped.Suggestions.Count, 1, PlanDefaults.MaxSuggestionsPerDay);
        Assert.All(result.Days.Where(day => !day.CalorieCapReached), day => Assert.NotEmpty(day.Meals));
    }

    [Fact]
    public async Task AddsWarningWhenConsumedDateIsOutsideThePlan()
    {
        var useCase = CreateUseCase(new StubChatModel(SelectionFor(7)), new StubKnowledgeStore());
        var request = Request() with { ConsumedCalories = 2500, ConsumedDate = "2026-11-10" };

        var result = await useCase.ExecuteAsync(request, CancellationToken.None);

        Assert.Contains(result.Warnings, warning => warning.Code == PlanWarningCodes.ConsumedDateOutsidePlan);
        Assert.DoesNotContain(result.Days, day => day.CalorieCapReached);
    }

    [Fact]
    public async Task AddsWarningForUnsupportedLocale()
    {
        var useCase = CreateUseCase(new StubChatModel(SelectionFor(7)), new StubKnowledgeStore());

        var result = await useCase.ExecuteAsync(Request() with { Locale = "en-US" }, CancellationToken.None);

        Assert.Contains(result.Warnings, warning => warning.Code == PlanWarningCodes.UnsupportedLocale);
        Assert.Equal(7, result.Days.Count);
    }

    [Fact]
    public async Task FallsBackWithTargetNotMetWhenCandidatesAreInsufficient()
    {
        var useCase = CreateUseCase(new StubChatModel(SelectionFor(1)), new StubKnowledgeStore());
        var request = Request() with
        {
            CandidateFoods = [Food("f-tiny", calories: 10, protein: 1, carbs: 1, fat: 1)],
        };

        var result = await useCase.ExecuteAsync(request, CancellationToken.None);

        Assert.True(result.UsedFallback);
        Assert.Contains(result.Warnings, warning => warning.Code == PlanWarningCodes.AiFallbackUsed);
        Assert.Contains(result.Warnings, warning => warning.Code == PlanWarningCodes.TargetNotMet);
    }

    [Fact]
    public async Task UsesOnlyCandidateFoodsAndNoMedicalAdvice()
    {
        var useCase = CreateUseCase(new StubChatModel(SelectionFor(7)), new StubKnowledgeStore());
        var request = Request();
        var catalog = request.CandidateFoods.Select(food => food.FoodId).ToHashSet(StringComparer.Ordinal);

        var result = await useCase.ExecuteAsync(request, CancellationToken.None);

        Assert.All(
            result.Days.SelectMany(day => day.Meals).SelectMany(meal => meal.Foods),
            food => Assert.Contains(food.FoodId, catalog));

        var text = string.Join(
            ' ',
            result.Days.SelectMany(day => day.Meals).Select(meal => meal.Rationale).Append(result.Disclaimer));
        Assert.DoesNotContain("chẩn đoán", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("điều trị", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("liều thuốc", text, StringComparison.OrdinalIgnoreCase);
    }

    private static GenerateMealPlanUseCase CreateUseCase(IChatModel chatModel, IKnowledgeStore knowledgeStore) =>
        new(
            new PlanRequestValidator(),
            new MealPlanCandidateFilter(new PlanAllergenFilter(), new DietaryPreferenceFilter()),
            new WeeklyBudgetSplitter(new PlanDateRange()),
            new MealPlanPromptBuilder(new PlanPromptFormatter()),
            new MealPlanSelectionParser(new CitationResolver(), new PlanRationaleBuilder(new SafetyGuard())),
            new MealPlanValidator(new PlanAllergenFilter()),
            new MealPlanFallbackSelector(new PlanRationaleBuilder(new SafetyGuard())),
            new MealPlanSuggestionBuilder(),
            knowledgeStore,
            chatModel);

    private static GenerateMealPlanRequest Request() => new()
    {
        Profile = Profile(),
        TargetCalories = 2000,
        TargetProteinGrams = 100,
        TargetCarbsGrams = 250,
        TargetFatGrams = 67,
        TargetFiberGrams = 30,
        WeekStartDate = WeekStart,
        Days = 7,
        MealsPerDay = 1,
        CandidateFoods =
        [
            Food("f-exact", calories: 2000, protein: 100, carbs: 250, fat: 67),
            Food("f-rice", calories: 230, protein: 5, carbs: 45, fat: 2),
            Food("f-tofu", calories: 180, protein: 14, carbs: 8, fat: 9),
        ],
        Locale = "vi-VN",
    };

    private static UserProfile Profile() => new()
    {
        Age = 35,
        Sex = "FEMALE",
        HeightCm = 165,
        WeightKg = 60,
        ActivityLevel = "ModeratelyActive",
        DietaryPreference = "None",
        Allergies = [],
    };

    private static CandidateFood Food(
        string foodId,
        int calories,
        double protein = 10,
        double carbs = 25,
        double fat = 6,
        IReadOnlyList<string>? allergens = null) => new()
        {
            FoodId = foodId,
            Name = $"Món {foodId}",
            ServingSize = "1",
            ServingUnit = "phần",
            Calories = calories,
            ProteinGrams = protein,
            CarbsGrams = carbs,
            FatGrams = fat,
            FiberGrams = 5,
            Allergens = allergens ?? [],
        };

    private static ChatCompletion SelectionFor(int days)
    {
        var dates = new[]
        {
            "2026-10-26", "2026-10-27", "2026-10-28", "2026-10-29",
            "2026-10-30", "2026-10-31", "2026-11-01",
        };

        var payloadDays = new JsonArray();
        for (var index = 0; index < days; index++)
        {
            payloadDays.Add(new JsonObject
            {
                ["date"] = dates[index],
                ["meals"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["meal_type"] = "LUNCH",
                        ["food_indices"] = new JsonArray(0),
                        ["citations"] = new JsonArray(),
                    },
                },
            });
        }

        return new ChatCompletion { Payload = new JsonObject { ["days"] = payloadDays } };
    }

    private static RetrievedChunk Chunk(int chapter, int page, string content) => new()
    {
        Chunk = new KnowledgeChunk
        {
            CorpusId = Guid.NewGuid(),
            ChapterNumber = chapter,
            ChapterTitle = $"Chương {chapter}",
            Content = content,
            BookPageFrom = page,
            BookPageTo = page,
            PdfPageFrom = page,
            PdfPageTo = page,
            Verified = true,
            SourceFile = $"chapter-{chapter}.md",
        },
        Score = 0.9,
    };

    private sealed class StubChatModel : IChatModel
    {
        private readonly ChatCompletion? _completion;
        private readonly bool _throwOnCall;

        public StubChatModel(ChatCompletion? completion = null, bool throwOnCall = false)
        {
            _completion = completion;
            _throwOnCall = throwOnCall;
        }

        public List<StructuredPrompt> Prompts { get; } = [];

        public Task<ChatCompletion> CompleteAsync(StructuredPrompt prompt, CancellationToken cancellationToken)
        {
            Prompts.Add(prompt);

            if (_throwOnCall)
            {
                throw new InvalidOperationException("transport failure");
            }

            return Task.FromResult(
                _completion ?? new ChatCompletion { Abstained = true, AbstainReason = "provider_unavailable" });
        }
    }

    private sealed class StubKnowledgeStore(IReadOnlyList<RetrievedChunk>? chunks = null) : IKnowledgeStore
    {
        private readonly IReadOnlyList<RetrievedChunk> _chunks = chunks ?? [];

        public Task<IReadOnlyList<RetrievedChunk>> SearchAsync(
            KnowledgeQuery query,
            CancellationToken cancellationToken)
        {
            var result = query.ChapterNumber == 0
                ? _chunks
                : _chunks.Where(chunk => chunk.Chunk.ChapterNumber == query.ChapterNumber).ToList();

            return Task.FromResult<IReadOnlyList<RetrievedChunk>>(result);
        }

        public Task<int> UpsertAsync(IReadOnlyList<KnowledgeChunk> chunks, CancellationToken cancellationToken) =>
            Task.FromResult(0);

        public Task<int> CountAsync(string corpusKey, CancellationToken cancellationToken) =>
            Task.FromResult(0);

        public Task<int> CountActiveAsync(CancellationToken cancellationToken) =>
            Task.FromResult(0);
    }
}
