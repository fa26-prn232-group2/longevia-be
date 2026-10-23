using Recommendation.Application.Features.Meals;
using Recommendation.Domain.Models;

namespace Recommendation.UnitTests.Features.Meals;

/// <summary>
/// T049 — lọc theo sở thích ăn uống đã khai báo (FR-003): Vegan loại thực phẩm
/// động vật, Keto loại món vượt ngưỡng carbohydrate.
/// </summary>
public sealed class DietaryPreferenceFilterTests
{
    private readonly DietaryPreferenceFilter _filter = new();

    [Theory]
    [InlineData("Thịt bò xào rau củ")]
    [InlineData("Trứng luộc")]
    [InlineData("Sữa chua Hy Lạp")]
    [InlineData("Cá hồi áp chảo")]
    public void VeganRemovesAnimalProducts(string name)
    {
        CandidateFood[] foods = [Food("f-animal", name, carbs: 2), Food("f-tofu", "Đậu hũ sốt cà chua", carbs: 4)];

        var result = _filter.Filter(foods, "Vegan");

        var remaining = Assert.Single(result);
        Assert.Equal("f-tofu", remaining.FoodId);
    }

    [Fact]
    public void VeganKeepsPlantBasedFood()
    {
        CandidateFood[] foods = [Food("f-tofu", "Đậu hũ sốt cà chua", carbs: 4)];

        var result = _filter.Filter(foods, "vegan");

        Assert.Single(result);
    }

    [Fact]
    public void KetoRemovesFoodAboveCarbLimit()
    {
        CandidateFood[] foods = [Food("f-rice", "Cơm trắng", carbs: 45), Food("f-chicken", "Ức gà nướng", carbs: 1)];

        var result = _filter.Filter(foods, "Keto");

        var remaining = Assert.Single(result);
        Assert.Equal("f-chicken", remaining.FoodId);
    }

    [Fact]
    public void KetoKeepsFoodAtCarbLimit()
    {
        CandidateFood[] foods = [Food("f-avocado", "Bơ quả", carbs: 10)];

        var result = _filter.Filter(foods, "keto");

        Assert.Single(result);
    }

    [Fact]
    public void LowCarbRemovesFoodAboveCarbLimit()
    {
        CandidateFood[] foods = [Food("f-rice", "Cơm trắng", carbs: 45), Food("f-avocado", "Bơ quả", carbs: 20)];

        var result = _filter.Filter(foods, "LowCarb");

        var remaining = Assert.Single(result);
        Assert.Equal("f-avocado", remaining.FoodId);
    }

    [Fact]
    public void NoPreferenceKeepsEveryFood()
    {
        CandidateFood[] foods = [Food("f-rice", "Cơm trắng", carbs: 45), Food("f-beef", "Thịt bò xào", carbs: 2)];

        var result = _filter.Filter(foods, "None");

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void MediterraneanDoesNotRemoveFood()
    {
        CandidateFood[] foods = [Food("f-rice", "Cơm trắng", carbs: 45), Food("f-fish", "Cá hồi áp chảo", carbs: 0)];

        var result = _filter.Filter(foods, "Mediterranean");

        Assert.Equal(2, result.Count);
    }

    private static CandidateFood Food(string foodId, string name, double carbs) => new()
    {
        FoodId = foodId,
        Name = name,
        Calories = 200,
        ProteinGrams = 10,
        CarbsGrams = carbs,
        FatGrams = 6,
        FiberGrams = 3,
    };
}
