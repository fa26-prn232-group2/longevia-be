using System.Text.Json.Nodes;
using Recommendation.Application.Abstractions;
using Recommendation.Application.Features.Ask;
using Recommendation.Application.Features.Meals;
using Recommendation.Domain.Constants;
using Recommendation.Domain.Models;

namespace Recommendation.UnitTests.Features.Meals;

/// <summary>
/// T050 — use case gợi ý bữa ăn: tối đa 5 món, dinh dưỡng lấy từ danh mục,
/// food_id thuộc danh mục, luôn có disclaimer và phục vụ đủ 4 bữa (FR-007).
/// </summary>
public sealed class GetMealRecommendationsUseCaseTests
{
    private const int CandidateCount = 10;

    [Fact]
    public async Task ReturnsAtMostFiveMeals()
    {
        var chatModel = new StubChatModel(SelectionPayload(CandidateCount));

        var result = await CreateUseCase(chatModel).ExecuteAsync(Request(), CancellationToken.None);

        Assert.Equal(MealSelectionParser.MaxMealsPerSlot, result.Meals.Count);
    }

    [Fact]
    public async Task KeepsNutritionFromCandidateFoodInsteadOfModel()
    {
        var chatModel = new StubChatModel(SelectionPayload(2));

        var result = await CreateUseCase(chatModel).ExecuteAsync(Request(), CancellationToken.None);

        Assert.All(result.Meals, meal =>
        {
            var candidate = Request().CandidateFoods.First(food => food.FoodId == meal.FoodId);
            Assert.Equal(candidate.Calories, meal.EstimatedCalories);
            Assert.Equal(candidate.ProteinGrams, meal.ProteinGrams, 3);
            Assert.Equal(candidate.CarbsGrams, meal.CarbsGrams, 3);
            Assert.Equal(candidate.FatGrams, meal.FatGrams, 3);
        });
    }

    [Fact]
    public async Task ReturnsOnlyFoodIdentifiersPresentInCatalog()
    {
        var chatModel = new StubChatModel(SelectionPayload(CandidateCount));
        var catalog = Request().CandidateFoods.Select(food => food.FoodId).ToHashSet(StringComparer.Ordinal);

        var result = await CreateUseCase(chatModel).ExecuteAsync(Request(), CancellationToken.None);

        Assert.All(result.Meals, meal => Assert.Contains(meal.FoodId, catalog));
    }

    [Fact]
    public async Task AlwaysReturnsDisclaimer()
    {
        var chatModel = new StubChatModel(SelectionPayload(1));

        var result = await CreateUseCase(chatModel).ExecuteAsync(Request(), CancellationToken.None);

        Assert.False(string.IsNullOrWhiteSpace(result.Disclaimer));
    }

    [Theory]
    [InlineData(MealSlot.Breakfast, "BREAKFAST")]
    [InlineData(MealSlot.Lunch, "LUNCH")]
    [InlineData(MealSlot.Dinner, "DINNER")]
    [InlineData(MealSlot.Snack, "SNACK")]
    public async Task SupportsEveryMealSlot(MealSlot slot, string expectedMarker)
    {
        var chatModel = new StubChatModel(SelectionPayload(2));
        var request = Request() with { MealSlot = slot };

        var result = await CreateUseCase(chatModel).ExecuteAsync(request, CancellationToken.None);

        var prompt = Assert.Single(chatModel.Prompts);
        Assert.Contains(expectedMarker, prompt.UserPrompt, StringComparison.Ordinal);
        Assert.NotEmpty(result.Meals);
    }

    [Fact]
    public async Task DoesNotCallModelWhenCatalogIsEmpty()
    {
        var chatModel = new StubChatModel(SelectionPayload(1));
        var request = Request() with { CandidateFoods = [] };

        var result = await CreateUseCase(chatModel).ExecuteAsync(request, CancellationToken.None);

        Assert.Empty(chatModel.Prompts);
        Assert.Empty(result.Meals);
        Assert.False(string.IsNullOrWhiteSpace(result.SafetyNote));
    }

    [Fact]
    public async Task SkipsMainMealsWhenCalorieCapIsReached()
    {
        var chatModel = new StubChatModel(SelectionPayload(2));
        var request = Request() with { ConsumedCalories = 2200, TargetCalories = 2000 };

        var result = await CreateUseCase(chatModel).ExecuteAsync(request, CancellationToken.None);

        Assert.True(result.CalorieCapReached);
        Assert.Empty(chatModel.Prompts);
        var drink = Assert.Single(result.Meals);
        Assert.Equal(string.Empty, drink.FoodId);
        Assert.Equal(0, drink.EstimatedCalories);
        Assert.False(string.IsNullOrWhiteSpace(result.LongevityTip));
        Assert.False(string.IsNullOrWhiteSpace(result.Disclaimer));
    }

    [Fact]
    public async Task ReturnsEmptyMealsWithSafetyNoteWhenModelAbstains()
    {
        var chatModel = new StubChatModel(null);

        var result = await CreateUseCase(chatModel).ExecuteAsync(Request(), CancellationToken.None);

        Assert.Empty(result.Meals);
        Assert.False(string.IsNullOrWhiteSpace(result.SafetyNote));
        Assert.False(string.IsNullOrWhiteSpace(result.Disclaimer));
    }

    [Fact]
    public async Task RemovesAllergenFoodBeforeCallingModel()
    {
        var chatModel = new StubChatModel(SelectionPayload(CandidateCount));
        var request = Request() with
        {
            Profile = new UserProfile { Allergies = ["đậu phộng"] },
            CandidateFoods =
            [
                Food("f-peanut", "Chè đậu phộng", allergens: ["đậu phộng"]),
                Food("f-rice", "Cơm gạo lứt"),
                Food("f-tofu", "Đậu hũ sốt cà chua"),
            ],
        };

        var result = await CreateUseCase(chatModel).ExecuteAsync(request, CancellationToken.None);

        var prompt = Assert.Single(chatModel.Prompts);
        Assert.DoesNotContain("f-peanut", prompt.UserPrompt, StringComparison.Ordinal);
        Assert.All(result.Meals, meal => Assert.NotEqual("f-peanut", meal.FoodId));
    }

    private static GetMealRecommendationsUseCase CreateUseCase(IChatModel chatModel) =>
        new(
            new AllergenGuard(),
            new DietaryPreferenceFilter(),
            new CalorieCapRule(),
            new MealSelector(
                new PromptBuilder(),
                chatModel,
                new StubKnowledgeStore(),
                new MealSelectionParser(new CitationResolver())));

    private static MealRecommendationRequest Request() => new()
    {
        MealSlot = MealSlot.Lunch,
        Goal = "LONGEVITY",
        Profile = new UserProfile { DietaryPreference = "None" },
        ConsumedCalories = 800,
        TargetCalories = 2000,
        TargetProteinRemaining = 30,
        TargetCarbsRemaining = 60,
        TargetFatRemaining = 15,
        CandidateFoods = Enumerable.Range(0, CandidateCount).Select(index => Food($"f-{index}", $"Món {index}")).ToList(),
        Locale = "vi-VN",
    };

    private static CandidateFood Food(string foodId, string name, IReadOnlyList<string>? allergens = null) => new()
    {
        FoodId = foodId,
        Name = name,
        Calories = 100,
        ProteinGrams = 10,
        CarbsGrams = 5,
        FatGrams = 3,
        FiberGrams = 2,
        Allergens = allergens ?? [],
    };

    private static ChatCompletion SelectionPayload(int count)
    {
        var selections = new JsonArray();
        for (var index = 0; index < count; index++)
        {
            selections.Add(new JsonObject
            {
                ["food_index"] = index,
                ["rationale"] = $"Lý do chọn món {index}.",
                ["citations"] = new JsonArray(),
            });
        }

        return new ChatCompletion { Payload = new JsonObject { ["selections"] = selections } };
    }

    private sealed class StubChatModel(ChatCompletion? completion) : IChatModel
    {
        public List<StructuredPrompt> Prompts { get; } = [];

        public Task<ChatCompletion> CompleteAsync(StructuredPrompt prompt, CancellationToken cancellationToken)
        {
            Prompts.Add(prompt);
            return Task.FromResult(completion ?? new ChatCompletion { Abstained = true, AbstainReason = "provider_unavailable" });
        }
    }

    private sealed class StubKnowledgeStore : IKnowledgeStore
    {
        public Task<IReadOnlyList<RetrievedChunk>> SearchAsync(KnowledgeQuery query, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<RetrievedChunk>>([]);

        public Task<int> UpsertAsync(IReadOnlyList<KnowledgeChunk> chunks, CancellationToken cancellationToken) =>
            Task.FromResult(0);

        public Task<int> CountAsync(string corpusKey, CancellationToken cancellationToken) =>
            Task.FromResult(0);
    }
}
