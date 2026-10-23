using Recommendation.Application.Features.Ask;
using Recommendation.Application.Features.MealPlans;
using Recommendation.Application.Features.Plans;
using Recommendation.Domain.Models;

namespace Recommendation.UnitTests.Features.Plans;

/// <summary>
/// T027 — FR-020/FR-021/SC-014: fallback deterministic chọn từ candidate, không
/// lặp món trong cùng ngày, vẫn trả kế hoạch khi provider lỗi và bật
/// <c>target_not_met</c> khi candidate không đủ.
/// </summary>
public sealed class MealPlanFallbackSelectorTests
{
    private readonly WeeklyBudgetSplitter _splitter = new(new PlanDateRange());
    private readonly MealPlanFallbackSelector _selector = new(new PlanRationaleBuilder(new SafetyGuard()));

    [Fact]
    public void ProducesAFoodForEveryMealOfEveryDay()
    {
        var request = Request(DiverseCandidates());
        var budget = _splitter.Split(request);

        var result = _selector.Select(request, budget, request.CandidateFoods);

        Assert.Equal(request.Days, result.Days.Count);
        Assert.All(result.Days, day =>
        {
            Assert.Equal(request.MealsPerDay, day.Meals.Count);
            Assert.All(day.Meals, meal => Assert.NotEmpty(meal.Foods));
        });
    }

    [Fact]
    public void NeverRepeatsFoodWithinTheSameDay()
    {
        var request = Request(DiverseCandidates());
        var budget = _splitter.Split(request);

        var result = _selector.Select(request, budget, request.CandidateFoods);

        Assert.All(result.Days, day =>
        {
            var ids = day.Meals.SelectMany(meal => meal.Foods).Select(food => food.FoodId).ToList();
            Assert.Equal(ids.Count, ids.Distinct(StringComparer.Ordinal).Count());
        });
    }

    [Fact]
    public void ReusesFoodWhenCandidatesAreFewerThanMealsWithoutLeavingMealsEmpty()
    {
        var request = Request([Food("f-only", "Món duy nhất", calories: 10, protein: 1, carbs: 1, fat: 1)]);
        var budget = _splitter.Split(request);

        var result = _selector.Select(request, budget, request.CandidateFoods);

        var day = Assert.Single(result.Days);
        Assert.Equal(request.MealsPerDay, day.Meals.Count);
        Assert.All(day.Meals, meal => Assert.NotEmpty(meal.Foods));
        Assert.All(
            day.Meals.SelectMany(meal => meal.Foods),
            food => Assert.Equal("f-only", food.FoodId));
        Assert.True(result.TargetNotMet);
    }

    [Fact]
    public void SetsTargetNotMetWhenCandidatesCannotReachTheBudget()
    {
        var request = Request([Food("f-tiny", "Món rất nhỏ", calories: 10, protein: 1, carbs: 1, fat: 1)]);
        var budget = _splitter.Split(request);

        var result = _selector.Select(request, budget, request.CandidateFoods);

        Assert.True(result.TargetNotMet);
    }

    [Fact]
    public void DoesNotSetTargetNotMetWhenCandidatesReachTheBudget()
    {
        var request = Request(ExactMealCandidates());
        var budget = _splitter.Split(request);

        var result = _selector.Select(request, budget, request.CandidateFoods);

        Assert.False(result.TargetNotMet);
        Assert.All(result.Days, day => Assert.All(day.Meals, meal => Assert.NotEmpty(meal.Foods)));
    }

    [Fact]
    public void IsDeterministicAcrossRuns()
    {
        var request = Request(DiverseCandidates());
        var budget = _splitter.Split(request);

        var first = _selector.Select(request, budget, request.CandidateFoods);
        var second = _selector.Select(request, budget, request.CandidateFoods);

        Assert.Equal(Describe(first), Describe(second));
    }

    [Fact]
    public void SkipsCappedDates()
    {
        var request = Request(DiverseCandidates(), days: 2);
        var budget = _splitter.Split(request);
        var skipped = new HashSet<string>(["2026-10-26"], StringComparer.Ordinal);

        var result = _selector.Select(request, budget, request.CandidateFoods, skipped);

        var day = Assert.Single(result.Days);
        Assert.Equal("2026-10-27", day.Date);
    }

    private static string Describe(MealPlanFallbackResult result) =>
        string.Join(
            '|',
            result.Days.Select(day => string.Join(
                ',',
                day.Meals.Select(meal => string.Join('-', meal.Foods.Select(food => food.FoodId))))));

    private static GenerateMealPlanRequest Request(
        IReadOnlyList<CandidateFood> candidates,
        int days = 1) => new()
        {
            Profile = new UserProfile
            {
                Age = 35,
                Sex = "FEMALE",
                HeightCm = 165,
                WeightKg = 60,
                ActivityLevel = "ModeratelyActive",
                DietaryPreference = "None",
                Allergies = [],
            },
            TargetCalories = 2000,
            TargetProteinGrams = 100,
            TargetCarbsGrams = 250,
            TargetFatGrams = 67,
            TargetFiberGrams = 30,
            WeekStartDate = "2026-10-26",
            Days = days,
            MealsPerDay = 4,
            CandidateFoods = candidates,
            Locale = "vi-VN",
        };

    private static IReadOnlyList<CandidateFood> ExactMealCandidates() =>
    [
        Food("f-breakfast", "Bữa sáng khớp", 500, 25, 62.5, 16.75),
        Food("f-lunch", "Bữa trưa khớp", 700, 35, 87.5, 23.45),
        Food("f-dinner", "Bữa tối khớp", 600, 30, 75, 20.1),
        Food("f-snack", "Bữa phụ khớp", 200, 10, 25, 6.7),
    ];

    private static IReadOnlyList<CandidateFood> DiverseCandidates() =>
    [
        Food("f-rice", "Cơm gạo lứt", 230, 5, 45, 2),
        Food("f-tofu", "Đậu hũ sốt cà", 180, 14, 8, 9),
        Food("f-salmon", "Cá hồi áp chảo", 250, 26, 0, 15),
        Food("f-chicken", "Ức gà luộc", 165, 31, 0, 4),
        Food("f-yogurt", "Sữa chua Hy Lạp", 120, 10, 6, 4),
        Food("f-oat", "Yến mạch", 150, 5, 27, 3),
        Food("f-egg", "Trứng luộc", 78, 6, 1, 5),
        Food("f-broccoli", "Bông cải xanh", 55, 4, 11, 1),
        Food("f-avocado", "Bơ", 160, 2, 9, 15),
        Food("f-banana", "Chuối", 105, 1, 27, 0),
        Food("f-beef", "Thịt bò nạc", 250, 26, 0, 15),
        Food("f-nuts", "Hạt óc chó", 185, 4, 4, 18),
    ];

    private static CandidateFood Food(
        string foodId,
        string name,
        int calories,
        double protein,
        double carbs,
        double fat) => new()
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
            Allergens = [],
        };
}
