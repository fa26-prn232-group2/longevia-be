using Recommendation.Application.Features.LongevityHabits;
using Recommendation.Domain.Constants;
using Recommendation.Domain.Models;

namespace Recommendation.UnitTests.Features.LongevityHabits;

/// <summary>
/// T100 — FR-012/FR-013: trọng số công khai của rubric (fasting 35 | sleep 30 |
/// hydration 20 | nutrition 15) cộng lại bằng 100, mỗi nhánh đạt tối đa đúng
/// ngưỡng (16 giờ, 7 giờ, 7/7 ngày, 90%), dưới ngưỡng thì thấp hơn, và cùng đầu
/// vào luôn cho cùng điểm.
/// </summary>
public sealed class LongevityRubricTests
{
    [Fact]
    public void WeightsMatchThePublishedDistributionAndSumToHundred()
    {
        Assert.Equal(35, LongevityRubric.FastingWeight);
        Assert.Equal(30, LongevityRubric.SleepWeight);
        Assert.Equal(20, LongevityRubric.HydrationWeight);
        Assert.Equal(15, LongevityRubric.NutritionWeight);
        Assert.Equal(100, LongevityRubric.TotalWeight);
    }

    [Fact]
    public void ReturnsTheFullScoreOnEveryBranchAtTheExactThresholds()
    {
        var score = Calculator().Calculate(Request(16d, 7d, 7, 90d));

        Assert.Equal(100, score.Score);
        Assert.Equal(
            [
                LongevityRubric.FastingKey,
                LongevityRubric.SleepKey,
                LongevityRubric.HydrationKey,
                LongevityRubric.NutritionKey,
            ],
            score.Components.Select(component => component.Key));
        Assert.All(score.Components, component => Assert.Equal(100, component.Score));
        Assert.Equal(100, score.Components.Sum(component => component.Weight));
        Assert.All(score.Components, component => Assert.False(string.IsNullOrWhiteSpace(component.Reason)));
    }

    [Fact]
    public void KeepsEveryBranchStrictlyBelowTheMaximumJustUnderTheThresholds()
    {
        var score = Calculator().Calculate(Request(15.9d, 6.9d, 6, 89.9d));

        Assert.All(score.Components, component => Assert.InRange(component.Score, 1, 99));
        Assert.Equal(99, Component(score, LongevityRubric.FastingKey).Score);
        Assert.Equal(98, Component(score, LongevityRubric.SleepKey).Score);
        Assert.Equal(85, Component(score, LongevityRubric.HydrationKey).Score);
        Assert.Equal(99, Component(score, LongevityRubric.NutritionKey).Score);
        Assert.Equal(96, score.Score);
    }

    [Fact]
    public void CapsEachBranchAtHundredWhenTheInputExceedsTheThreshold()
    {
        var score = Calculator().Calculate(Request(20d, 9d, 10, 100d));

        Assert.Equal(100, score.Score);
        Assert.All(score.Components, component => Assert.Equal(100, component.Score));
    }

    [Fact]
    public void FloorsEveryBranchAtZeroForMissingOrInvalidValues()
    {
        var score = Calculator().Calculate(Request(-4d, double.NaN, -2, -10d));

        Assert.Equal(0, score.Score);
        Assert.All(score.Components, component => Assert.Equal(0, component.Score));
    }

    [Fact]
    public void WeightsOnlyTheBranchThatReachesItsThreshold()
    {
        var score = Calculator().Calculate(Request(16d, 0d, 0, 0d));

        Assert.Equal(35, score.Score);
        Assert.Equal(100, Component(score, LongevityRubric.FastingKey).Score);
        Assert.Equal(0, Component(score, LongevityRubric.SleepKey).Score);
        Assert.Equal(0, Component(score, LongevityRubric.HydrationKey).Score);
        Assert.Equal(0, Component(score, LongevityRubric.NutritionKey).Score);
    }

    [Fact]
    public void ReturnsTheSameScoreAndReasonsForTheSameInput()
    {
        var calculator = Calculator();

        var first = calculator.Calculate(Request(14.5d, 6.5d, 5, 80d));
        var second = calculator.Calculate(Request(14.5d, 6.5d, 5, 80d));

        Assert.Equal(first.Score, second.Score);
        Assert.Equal(first.Components, second.Components);
    }

    private static LongevityRubricCalculator Calculator() => new();

    private static LongevityHabitRequest Request(
        double fastingHours,
        double sleepHours,
        int waterTargetHitDays,
        double nutritionAdherenceRate) => new()
        {
            UserId = "user-1",
            AverageFastingHours = fastingHours,
            AverageSleepHours = sleepHours,
            WaterTargetHitDays = waterTargetHitDays,
            NutritionAdherenceRate = nutritionAdherenceRate,
        };

    private static ScoreComponent Component(LongevityHabitScore score, string key) =>
        Assert.Single(score.Components, component => component.Key == key);
}
