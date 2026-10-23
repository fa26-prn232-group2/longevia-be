using Recommendation.Application.Features.MealPlans;
using Recommendation.Application.Features.Plans;
using Recommendation.Domain.Constants;
using Recommendation.Domain.Models;

namespace Recommendation.UnitTests.Features.Plans;

/// <summary>
/// T026 — FR-019: validator từ chối kế hoạch AI sai số ngày/bữa, quá 5 món/bữa,
/// món ngoài candidate, còn allergen hoặc lệch &gt; ±10% calo/protein/carbs/fat.
/// </summary>
public sealed class MealPlanValidatorTests
{
    private readonly WeeklyBudgetSplitter _splitter = new(new PlanDateRange());
    private readonly MealPlanValidator _validator = new(new PlanAllergenFilter());

    [Fact]
    public void AcceptsPlanThatMatchesTheDailyBudget()
    {
        var request = Request();
        var budget = _splitter.Split(request);
        var days = new[] { BuildDay(budget.Days[0], Food()) };

        var result = _validator.Validate(request, budget, days, request.CandidateFoods);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void RejectsMissingDay()
    {
        var request = Request(days: 2);
        var budget = _splitter.Split(request);

        var result = _validator.Validate(request, budget, [], request.CandidateFoods);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void RejectsMissingMeal()
    {
        var request = Request(mealsPerDay: 2);
        var budget = _splitter.Split(request);
        var day = BuildDayWithMeal(budget.Days[0], [Food()], budget.Days[0].Meals[0]);

        var result = _validator.Validate(request, budget, [day], request.CandidateFoods);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void RejectsMoreThanFiveFoodsPerMeal()
    {
        var request = Request();
        var budget = _splitter.Split(request);
        var foods = Enumerable.Range(0, PlanDefaults.MaxFoodsPerMeal + 1)
            .Select(index => Food($"f-{index}"))
            .ToList();
        var day = BuildDay(budget.Days[0], foods.ToArray());

        var result = _validator.Validate(request, budget, [day], request.CandidateFoods);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void RejectsFoodOutsideCandidateList()
    {
        var request = Request();
        var budget = _splitter.Split(request);
        var day = BuildDay(budget.Days[0], Food("f-outside"));

        var result = _validator.Validate(request, budget, [day], request.CandidateFoods);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void RejectsAllergenFoodThatSlippedThrough()
    {
        var allergenFood = Food(allergens: ["đậu phộng"]);
        var request = Request(candidates: [allergenFood]) with
        {
            Profile = Profile() with { Allergies = ["đậu phộng"] },
        };
        var budget = _splitter.Split(request);
        var day = BuildDay(budget.Days[0], allergenFood);

        var result = _validator.Validate(request, budget, [day], request.CandidateFoods);

        Assert.False(result.IsValid);
    }

    [Theory]
    [InlineData(2300, 100, 250, 67)]
    [InlineData(2000, 120, 250, 67)]
    [InlineData(2000, 100, 300, 67)]
    [InlineData(2000, 100, 250, 90)]
    public void RejectsDeviationBeyondTenPercent(
        int calories,
        double protein,
        double carbs,
        double fat)
    {
        var food = Food(calories: calories, protein: protein, carbs: carbs, fat: fat);
        var request = Request(candidates: [food]);
        var budget = _splitter.Split(request);
        var day = BuildDay(budget.Days[0], food);

        var result = _validator.Validate(request, budget, [day], request.CandidateFoods);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void FiberIsOnlyReportedAndNotGated()
    {
        var food = Food(fiber: 0);
        var request = Request(candidates: [food]);
        var budget = _splitter.Split(request);
        var day = BuildDay(budget.Days[0], food);

        var result = _validator.Validate(request, budget, [day], request.CandidateFoods);

        Assert.True(result.IsValid);
        Assert.Equal(0, day.TotalFiberGrams);
    }

    [Fact]
    public void SkipsCappedDays()
    {
        var request = Request();
        var budget = _splitter.Split(request);
        var skipped = new HashSet<string>(["2026-10-26"], StringComparer.Ordinal);

        var result = _validator.Validate(request, budget, [], request.CandidateFoods, skipped);

        Assert.True(result.IsValid);
    }

    private static GenerateMealPlanRequest Request(
        int days = 1,
        int mealsPerDay = 1,
        IReadOnlyList<CandidateFood>? candidates = null) => new()
        {
            Profile = Profile(),
            TargetCalories = 2000,
            TargetProteinGrams = 100,
            TargetCarbsGrams = 250,
            TargetFatGrams = 67,
            TargetFiberGrams = 30,
            WeekStartDate = "2026-10-26",
            Days = days,
            MealsPerDay = mealsPerDay,
            CandidateFoods = candidates ?? [Food()],
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
        string foodId = "f-1",
        int calories = 2000,
        double protein = 100,
        double carbs = 250,
        double fat = 67,
        double fiber = 30,
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
            FiberGrams = fiber,
            Allergens = allergens ?? [],
        };

    private static MealPlanDay BuildDay(
        WeeklyBudget.DayBudget budget,
        params CandidateFood[] foods) =>
        BuildDayWithMeal(budget, foods, budget.Meals[0]);

    private static MealPlanDay BuildDayWithMeal(
        WeeklyBudget.DayBudget budget,
        CandidateFood[] foods,
        WeeklyBudget.MealBudget mealBudget)
    {
        var plannedFoods = foods.Select(ToPlannedFood).ToList();
        var meal = new PlannedMeal
        {
            MealType = mealBudget.Slot,
            TargetCalories = mealBudget.TargetCalories,
            Foods = plannedFoods,
            Rationale = "Do code dựng.",
            Citations = [],
        };

        return new MealPlanDay
        {
            Date = budget.Date,
            TargetCalories = budget.TargetCalories,
            TargetProteinGrams = budget.TargetProteinGrams,
            TargetCarbsGrams = budget.TargetCarbsGrams,
            TargetFatGrams = budget.TargetFatGrams,
            TargetFiberGrams = budget.TargetFiberGrams,
            TotalCalories = plannedFoods.Sum(food => food.Calories),
            TotalProteinGrams = plannedFoods.Sum(food => food.ProteinGrams),
            TotalCarbsGrams = plannedFoods.Sum(food => food.CarbsGrams),
            TotalFatGrams = plannedFoods.Sum(food => food.FatGrams),
            TotalFiberGrams = plannedFoods.Sum(food => food.FiberGrams),
            Meals = [meal],
        };
    }

    private static PlannedFood ToPlannedFood(CandidateFood food) => new()
    {
        FoodId = food.FoodId,
        Name = food.Name,
        ServingSize = food.ServingSize,
        ServingUnit = food.ServingUnit,
        Calories = food.Calories,
        ProteinGrams = food.ProteinGrams,
        CarbsGrams = food.CarbsGrams,
        FatGrams = food.FatGrams,
        FiberGrams = food.FiberGrams,
        Allergens = food.Allergens,
    };
}
