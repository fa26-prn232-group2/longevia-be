using System.Text;
using Recommendation.Application.Features.MealPlans;
using Recommendation.Domain.Models;

namespace Recommendation.UnitTests.Features.Plans;

/// <summary>
/// T023 — FR-015/D-03: allergen của plan được chuẩn hoá trim + lowercase + NFC
/// và so khớp chính xác từng token; rỗng nghĩa là không có allergen.
/// </summary>
public sealed class PlanAllergenFilterTests
{
    private readonly PlanAllergenFilter _filter = new();

    [Fact]
    public void MatchesAfterTrimLowercaseAndNfc()
    {
        var decomposed = "đậu phộng".Normalize(NormalizationForm.FormD);
        var food = Food("f-peanut", allergens: [decomposed]);

        Assert.True(_filter.ContainsAllergen(food, ["  ĐẬU PHỘNG "]));
    }

    [Fact]
    public void EmptyAllergiesMeansNoAllergen()
    {
        var food = Food("f-peanut", allergens: ["đậu phộng"]);

        Assert.False(_filter.ContainsAllergen(food, []));
        Assert.Single(_filter.Filter([food], []));
    }

    [Fact]
    public void EmptyFoodAllergensIsTreatedAsSafe()
    {
        var food = Food("f-rice", allergens: []);

        Assert.False(_filter.ContainsAllergen(food, ["đậu phộng"]));
        Assert.Single(_filter.Filter([food], ["đậu phộng"]));
    }

    [Fact]
    public void DoesNotMatchSubstring()
    {
        var food = Food("f-peanut", allergens: ["đậu phộng rang"]);

        Assert.False(_filter.ContainsAllergen(food, ["đậu phộng"]));
    }

    [Fact]
    public void MatchesOnlyExactToken()
    {
        var food = Food("f-peanut", allergens: ["ĐẬU PHỘNG"]);

        Assert.True(_filter.ContainsAllergen(food, ["đậu phộng"]));
        Assert.False(_filter.ContainsAllergen(food, ["phộng"]));
    }

    [Fact]
    public void FilterKeepsSafeFoodsAndRemovesMatchingOnes()
    {
        var peanut = Food("f-peanut", allergens: ["đậu phộng"]);
        var rice = Food("f-rice", allergens: []);
        var milk = Food("f-milk", allergens: ["sữa"]);

        var filtered = _filter.Filter([peanut, rice, milk], ["đậu phộng"]);

        Assert.Equal(["f-rice", "f-milk"], filtered.Select(food => food.FoodId));
    }

    private static CandidateFood Food(string foodId, IReadOnlyList<string> allergens) => new()
    {
        FoodId = foodId,
        Name = foodId,
        Calories = 100,
        ProteinGrams = 10,
        CarbsGrams = 5,
        FatGrams = 3,
        FiberGrams = 2,
        Allergens = allergens,
    };
}
