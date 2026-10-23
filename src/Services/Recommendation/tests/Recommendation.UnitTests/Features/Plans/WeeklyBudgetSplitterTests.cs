using Recommendation.Application.Features.MealPlans;
using Recommendation.Application.Features.Plans;

namespace Recommendation.UnitTests.Features.Plans;

/// <summary>
/// T024 — FR-016/D-04: mục tiêu ngày được chia cho từng bữa theo tỷ lệ cố định;
/// tổng ngân sách bữa bằng ngân sách ngày, tổng ngân sách ngày bằng mục tiêu
/// ngày × số ngày, phần lẻ làm tròn dồn vào bữa cuối.
/// </summary>
public sealed class WeeklyBudgetSplitterTests
{
    private readonly WeeklyBudgetSplitter _splitter = new(new PlanDateRange());

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(3, 3)]
    [InlineData(4, 4)]
    public void ProducesTheRequestedNumberOfMeals(int mealsPerDay, int expectedMeals)
    {
        var budget = _splitter.Split(Request(mealsPerDay: mealsPerDay));

        var day = Assert.Single(budget.Days);
        Assert.Equal(expectedMeals, day.Meals.Count);
        Assert.Equal(2000, day.TargetCalories);
        Assert.Equal(day.TargetCalories, day.Meals.Sum(meal => meal.TargetCalories));
        Assert.Equal(100, day.TargetProteinGrams, 3);
        Assert.Equal(100, day.Meals.Sum(meal => meal.TargetProteinGrams), 3);
    }

    [Fact]
    public void EveryDayBudgetEqualsTheDailyTarget()
    {
        var budget = _splitter.Split(Request(days: 7, mealsPerDay: 4));

        Assert.Equal(7, budget.Days.Count);
        Assert.All(budget.Days, day => Assert.Equal(2000, day.TargetCalories));
        Assert.Equal(14_000, budget.Days.Sum(day => day.TargetCalories));
        Assert.Equal(700, budget.Days.Sum(day => day.TargetProteinGrams), 3);
    }

    [Fact]
    public void RoundingRemainderGoesToTheLastMeal()
    {
        var budget = _splitter.Split(Request(targetCalories: 1001, mealsPerDay: 3));

        var day = Assert.Single(budget.Days);
        Assert.Equal([300, 400, 301], day.Meals.Select(meal => meal.TargetCalories));
        Assert.Equal(1001, day.Meals.Sum(meal => meal.TargetCalories));
    }

    [Fact]
    public void DatesAreConsecutiveAndKeepTheWeekStart()
    {
        var budget = _splitter.Split(Request(days: 3));

        Assert.Equal(["2026-10-26", "2026-10-27", "2026-10-28"], budget.Days.Select(day => day.Date));
    }

    private static GenerateMealPlanRequest Request(
        int targetCalories = 2000,
        int days = 1,
        int mealsPerDay = 3) => new()
        {
            TargetCalories = targetCalories,
            TargetProteinGrams = 100,
            TargetCarbsGrams = 250,
            TargetFatGrams = 67,
            TargetFiberGrams = 30,
            WeekStartDate = "2026-10-26",
            Days = days,
            MealsPerDay = mealsPerDay,
        };
}
