using Longevia.Recommendation.Contracts.V1;
using Recommendation.IntegrationTests.Features.Meals;

namespace Recommendation.IntegrationTests.Features.LongevityHabits;

/// <summary>
/// T102 — FR-012/FR-013: gọi RPC thật qua host Kestrel + PostgreSQL thật; điểm
/// tổng nằm trong 0..100, score_breakdown có đủ bốn nhánh và tổng trọng số bằng
/// 100. Rubric chạy bằng code nên kết quả không phụ thuộc provider AI.
/// </summary>
public sealed class EvaluateLongevityHabitsGrpcTests(RecommendationApiFixture fixture)
    : IClassFixture<RecommendationApiFixture>
{
    [Fact]
    public async Task ReturnsTheFullScoreAndFourWeightedComponentsAtTheThresholds()
    {
        var response = await fixture.Client.EvaluateLongevityHabitsAsync(
            BuildRequest(fastingHours: 16d, sleepHours: 7d, waterDays: 7, nutritionRate: 90d));

        Assert.Equal(100, response.LongevityScore);
        AssertScoreBreakdown(response);
        Assert.All(response.ScoreBreakdown, component => Assert.Equal(100, component.Score));
        Assert.False(string.IsNullOrWhiteSpace(response.Disclaimer.Text));
        Assert.NotEmpty(response.ActionableInsights);
    }

    [Fact]
    public async Task ReturnsALowerButStillValidScoreWhenEveryInputIsBelowTheThreshold()
    {
        var response = await fixture.Client.EvaluateLongevityHabitsAsync(
            BuildRequest(fastingHours: 15.9d, sleepHours: 6.9d, waterDays: 6, nutritionRate: 89.9d));

        Assert.InRange(response.LongevityScore, 0, 99);
        AssertScoreBreakdown(response);
        Assert.All(response.ScoreBreakdown, component => Assert.InRange(component.Score, 1, 99));
    }

    private static void AssertScoreBreakdown(LongevityHabitResponse response)
    {
        Assert.Equal(4, response.ScoreBreakdown.Count);
        Assert.Equal(100, response.ScoreBreakdown.Sum(component => component.Weight));
        Assert.Equal(
            ["fasting", "sleep", "hydration", "nutrition"],
            response.ScoreBreakdown.Select(component => component.Key));
        Assert.All(response.ScoreBreakdown, component => Assert.False(string.IsNullOrWhiteSpace(component.Reason)));
    }

    private static LongevityHabitRequest BuildRequest(
        double fastingHours,
        double sleepHours,
        int waterDays,
        double nutritionRate) => new()
        {
            UserId = "user-integration",
            AverageFastingHours = fastingHours,
            AverageSleepHours = sleepHours,
            WaterTargetHitDays = waterDays,
            NutritionAdherenceRate = nutritionRate,
            Profile = new UserProfile
            {
                Age = 30,
                Sex = BiologicalSex.Female,
            },
        };
}
