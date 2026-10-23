using Recommendation.Application.Features.MealPlans;
using Recommendation.Application.Features.Meals;
using Recommendation.Domain.Constants;
using Recommendation.Domain.Models;

namespace Recommendation.UnitTests.Features.Plans;

/// <summary>
/// T025 — FR-015/FR-018/FR-025: dedupe <c>food_id</c>, cắt &gt; 120 theo macro-fit,
/// lọc allergen/dietary và loại <c>excluded_food_ids</c> trước khi gọi AI.
/// </summary>
public sealed class MealPlanCandidateFilterTests
{
    private readonly MealPlanCandidateFilter _filter = new(new PlanAllergenFilter(), new DietaryPreferenceFilter());

    [Fact]
    public void DedupesFoodIdAndWarns()
    {
        var request = Request(
            [
                Food("f-rice", "Cơm gạo lứt", calories: 230),
                Food("f-rice", "Cơm gạo lứt (bản lặp)", calories: 999),
                Food("f-tofu", "Đậu hũ", calories: 180),
            ]);

        var result = _filter.Filter(request);

        Assert.Equal(["f-rice", "f-tofu"], result.Candidates.Select(food => food.FoodId));
        Assert.Contains(result.Warnings, warning => warning.Code == PlanWarningCodes.DuplicateCandidateFood);
    }

    [Fact]
    public void TruncatesMoreThan120CandidatesByMacroFitAndWarns()
    {
        var candidates = Enumerable.Range(0, 121)
            .Select(index => Food($"food-{index:000}", $"Món {index}", calories: 200))
            .ToList();

        var result = _filter.Filter(Request(candidates));

        Assert.Equal(PlanDefaults.MaxCandidateFoods, result.Candidates.Count);
        Assert.DoesNotContain(result.Candidates, food => food.FoodId == "food-120");
        Assert.Contains(result.Warnings, warning => warning.Code == PlanWarningCodes.CandidateFoodsTruncated);
    }

    [Fact]
    public void KeepsMacroFittingCandidatesWhenTrimming()
    {
        var candidates = Enumerable.Range(0, 120)
            .Select(index => Food($"poor-{index:000}", $"Món {index}", calories: 1200))
            .ToList();
        candidates.Add(Food("fit-000", "Món khớp", calories: 200, protein: 10, carbs: 25, fat: 6));

        var result = _filter.Filter(Request(candidates));

        Assert.Equal(PlanDefaults.MaxCandidateFoods, result.Candidates.Count);
        Assert.Contains(result.Candidates, food => food.FoodId == "fit-000");
        Assert.DoesNotContain(result.Candidates, food => food.FoodId == "poor-119");
    }

    [Fact]
    public void RemovesAllergenFoods()
    {
        var request = Request(
            [
                Food("f-peanut", "Chè đậu phộng", allergens: ["đậu phộng"]),
                Food("f-rice", "Cơm gạo lứt"),
            ]) with
        {
            Profile = Profile() with { Allergies = ["đậu phộng"] },
        };

        var result = _filter.Filter(request);

        Assert.Equal(["f-rice"], result.Candidates.Select(food => food.FoodId));
    }

    [Fact]
    public void RemovesFoodsAgainstDietaryPreference()
    {
        var request = Request(
            [
                Food("f-beef", "Thịt bò nướng"),
                Food("f-rice", "Cơm gạo lứt"),
            ]) with
        {
            Profile = Profile() with { DietaryPreference = "Vegan" },
        };

        var result = _filter.Filter(request);

        Assert.Equal(["f-rice"], result.Candidates.Select(food => food.FoodId));
    }

    [Fact]
    public void RemovesExcludedFoodIds()
    {
        var request = Request(
            [
                Food("f-rice", "Cơm gạo lứt"),
                Food("f-tofu", "Đậu hũ"),
            ]) with
        {
            ExcludedFoodIds = ["f-tofu"],
        };

        var result = _filter.Filter(request);

        Assert.Equal(["f-rice"], result.Candidates.Select(food => food.FoodId));
    }

    private static GenerateMealPlanRequest Request(IReadOnlyList<CandidateFood> candidates) => new()
    {
        Profile = Profile(),
        TargetCalories = 2000,
        TargetProteinGrams = 100,
        TargetCarbsGrams = 250,
        TargetFatGrams = 67,
        WeekStartDate = "2026-10-26",
        Days = 7,
        MealsPerDay = 4,
        CandidateFoods = candidates,
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
        string name,
        int calories = 200,
        double protein = 10,
        double carbs = 25,
        double fat = 6,
        IReadOnlyList<string>? allergens = null) => new()
        {
            FoodId = foodId,
            Name = name,
            ServingSize = "1",
            ServingUnit = "phần",
            Calories = calories,
            ProteinGrams = protein,
            CarbsGrams = carbs,
            FatGrams = fat,
            FiberGrams = 3,
            Allergens = allergens ?? [],
        };
}
