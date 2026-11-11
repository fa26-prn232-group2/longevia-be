using System.Text.Json.Nodes;
using Recommendation.Application.Abstractions;
using Recommendation.Application.Features.Ask;
using Recommendation.Application.Features.Targets;
using Recommendation.Domain.Models;

namespace Recommendation.UnitTests.Features.Targets;

public sealed class SuggestNutritionTargetsUseCaseTests
{
    private static readonly UserProfile Profile = new()
    {
        Age = 30,
        Sex = "MALE",
        HeightCm = 180,
        WeightKg = 80,
        ActivityLevel = "ModeratelyActive",
    };

    [Fact]
    public async Task UsesAiMacrosWhenInsideSafeRange()
    {
        var useCase = Create(Tip(protein: 150, fat: 70, rationale: "Chia đều 3 bữa."));

        var result = await useCase.ExecuteAsync(Request(), CancellationToken.None);

        Assert.Equal("ai", result.Source);
        Assert.Equal(2760, result.Calories);
        Assert.Equal(150, result.ProteinGrams);
        Assert.Equal(70, result.FatGrams);
        Assert.Equal(382.5, result.CarbsGrams);
        Assert.Contains("Chia đều", result.Rationale, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FallsBackToFormulaWhenAiMacrosAreUnsafe()
    {
        var useCase = Create(Tip(protein: 500, fat: 400, rationale: "quá cao"));

        var result = await useCase.ExecuteAsync(Request(), CancellationToken.None);

        Assert.Equal("formula", result.Source);
        Assert.Equal(2760, result.Calories);
        Assert.Equal(112, result.ProteinGrams);
        Assert.Contains("Cách tính", result.Rationale, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FallsBackToFormulaWhenProviderAbstains()
    {
        var useCase = Create(new ChatCompletion { Abstained = true, AbstainReason = "provider_timeout" });

        var result = await useCase.ExecuteAsync(Request(), CancellationToken.None);

        Assert.Equal("formula", result.Source);
        Assert.Equal(2760, result.Calories);
        Assert.Contains("công thức", result.SafetyNote, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Formula_ForOtherSex_UsesNeutralAdjustment()
    {
        var useCase = Create(new ChatCompletion { Abstained = true, AbstainReason = "provider_timeout" });
        var request = Request() with { Profile = Profile with { Sex = "OTHER" } };

        var result = await useCase.ExecuteAsync(request, CancellationToken.None);

        Assert.Equal("formula", result.Source);
        // BMR = 10*80 + 6.25*180 - 5*30 - 78 = 1697; TDEE = 1697*1.55 = 2630.35 -> 2630 kcal.
        Assert.Equal(2630, result.Calories);
        Assert.Equal(112, result.ProteinGrams);
    }

    [Fact]
    public async Task RejectsUnsupportedProfile()
    {
        var useCase = Create(Tip(150, 70, "ok"));
        var request = Request() with { Profile = Profile with { WeightKg = 10 } };

        await Assert.ThrowsAsync<Longevia.Shared.Errors.AppException>(() =>
            useCase.ExecuteAsync(request, CancellationToken.None));
    }

    private static SuggestNutritionTargetsRequest Request() =>
        new(Profile, "MAINTAIN", "Tôi muốn giữ cân và tập 3 buổi/tuần.", 2500);

    private static SuggestNutritionTargetsUseCase Create(ChatCompletion completion) =>
        new(new PromptBuilder(), new StubChatModel(completion));

    private static ChatCompletion Tip(double protein, double fat, string rationale) => new()
    {
        Payload = new JsonObject
        {
            ["protein_grams"] = protein,
            ["fat_grams"] = fat,
            ["rationale"] = rationale,
        },
    };

    private sealed class StubChatModel(ChatCompletion completion) : IChatModel
    {
        public Task<ChatCompletion> CompleteAsync(StructuredPrompt prompt, CancellationToken cancellationToken) =>
            Task.FromResult(completion);
    }
}
