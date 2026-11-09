using Diet.Domain;
using Diet.Domain.Entities;

namespace Diet.UnitTests;

public sealed class MealPlanAndMicronutrientTests
{
    [Fact]
    public void FoodItem_StoresMicronutrientsAndRejectsNegativeValues()
    {
        var food = FoodItem.Create(null, "Cá hồi", "Seafood", 100, "g",
            new NutritionValues(208, 20, 0, 13, 0), ["fish"],
            new Dictionary<string, decimal> { ["Vitamin D"] = 11m, ["Omega-3"] = 1.8m });
        Assert.Equal(11m, food.Micronutrients!["Vitamin D"]);

        Assert.Throws<ArgumentException>(() => FoodItem.Create(null, "X", "Y", 100, "g",
            new NutritionValues(1, 0, 0, 0, 0), null,
            new Dictionary<string, decimal> { ["Vitamin D"] = -1m }));
    }

    [Fact]
    public void MealPlan_RequiresMondayAndValidRanges()
    {
        var plan = MealPlan.Create(Guid.NewGuid(), new DateOnly(2026, 11, 9), 7, 4, "{}", DateTimeOffset.UtcNow);
        Assert.Equal(new DateOnly(2026, 11, 9), plan.WeekStartDate);

        Assert.Throws<ArgumentException>(() => MealPlan.Create(Guid.NewGuid(),
            new DateOnly(2026, 11, 10), 7, 4, "{}", DateTimeOffset.UtcNow));
        Assert.Throws<ArgumentOutOfRangeException>(() => MealPlan.Create(Guid.NewGuid(),
            new DateOnly(2026, 11, 9), 8, 4, "{}", DateTimeOffset.UtcNow));
        Assert.Throws<ArgumentOutOfRangeException>(() => MealPlan.Create(Guid.NewGuid(),
            new DateOnly(2026, 11, 9), 7, 5, "{}", DateTimeOffset.UtcNow));
    }
}
