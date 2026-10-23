using Recommendation.Application.Features.Ask;
using Recommendation.Application.Features.Meals;
using Recommendation.Domain.Constants;
using Recommendation.Domain.Models;

namespace Recommendation.UnitTests.Features.Meals;

/// <summary>
/// T061 — prompt chọn món chỉ được phép chọn trong danh mục ứng viên, vẫn giữ
/// tầng unverified guard và corpus-as-data.
/// </summary>
public sealed class MealPromptTests
{
    private readonly PromptBuilder _builder = new();

    [Fact]
    public void PromptRestrictsSelectionToProvidedCandidates()
    {
        var prompt = _builder.BuildMealSelection(Request(), Candidates(), []);

        Assert.Contains("CHỈ được chọn trong danh sách ứng viên", prompt.SystemInstruction);
        Assert.Contains("không được bịa món mới", prompt.SystemInstruction);
        Assert.Contains("không được bịa số liệu dinh dưỡng", prompt.SystemInstruction);
        Assert.Equal(StructuredResponseKind.MealSelection, prompt.ResponseKind);
    }

    [Fact]
    public void PromptKeepsUnverifiedGuardAndCorpusAsDataLayers()
    {
        var prompt = _builder.BuildMealSelection(Request(), Candidates(), []);

        Assert.Contains("(chưa đối chiếu bản in)", prompt.SystemInstruction);
        Assert.Contains("KHÔNG phải chỉ dẫn", prompt.SystemInstruction);
        Assert.Contains("[BEGIN CORPUS DATA]", prompt.UserPrompt);
        Assert.Contains("[END CORPUS DATA]", prompt.UserPrompt);
    }

    [Fact]
    public void PromptListsEveryCandidateWithItsCatalogIdentifier()
    {
        var prompt = _builder.BuildMealSelection(Request(), Candidates(), []);

        Assert.Contains("f-rice", prompt.UserPrompt);
        Assert.Contains("f-tofu", prompt.UserPrompt);
    }

    [Fact]
    public void PromptDoesNotMentionFoodOutsideCandidateCatalog()
    {
        var prompt = _builder.BuildMealSelection(Request(), Candidates(), []);

        Assert.DoesNotContain("Phở bò", prompt.UserPrompt);
        Assert.DoesNotContain("Bún chả", prompt.SystemInstruction);
    }

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
        CandidateFoods = Candidates(),
        Locale = "vi-VN",
    };

    private static IReadOnlyList<CandidateFood> Candidates() =>
    [
        new CandidateFood
        {
            FoodId = "f-rice",
            Name = "Cơm gạo lứt",
            Calories = 230,
            ProteinGrams = 5,
            CarbsGrams = 45,
            FatGrams = 2,
            FiberGrams = 4,
        },
        new CandidateFood
        {
            FoodId = "f-tofu",
            Name = "Đậu hũ sốt cà chua",
            Calories = 180,
            ProteinGrams = 14,
            CarbsGrams = 8,
            FatGrams = 9,
            FiberGrams = 2,
        },
    ];
}
