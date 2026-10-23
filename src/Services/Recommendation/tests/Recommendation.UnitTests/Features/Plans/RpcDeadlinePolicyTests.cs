using Microsoft.Extensions.Options;
using Recommendation.Api.Deadlines;
using Recommendation.Infrastructure.Options;

namespace Recommendation.UnitTests.Features.Plans;

/// <summary>
/// T003 — D-02/FR-054: hai RPC plan dùng deadline riêng (mặc định 90s), 5 RPC
/// cũ giữ deadline chung (mặc định 30s).
/// </summary>
public sealed class RpcDeadlinePolicyTests
{
    private const string ServiceMethodPrefix =
        "/longevia.recommendation.v1.RecommendationService/";

    [Theory]
    [InlineData("GenerateMealPlan")]
    [InlineData("GenerateWorkoutPlan")]
    public void PlanMethodsUsePlanDeadline(string methodName)
    {
        var policy = CreatePolicy();

        var deadline = policy.Resolve(ServiceMethodPrefix + methodName);

        Assert.Equal(TimeSpan.FromSeconds(90), deadline);
    }

    [Theory]
    [InlineData("GetMealRecommendations")]
    [InlineData("AnalyzeNutrientGaps")]
    [InlineData("EvaluateLongevityHabits")]
    [InlineData("AskLongevityQuestion")]
    [InlineData("SearchLongevityKnowledge")]
    public void ExistingMethodsKeepDefaultDeadline(string methodName)
    {
        var policy = CreatePolicy();

        var deadline = policy.Resolve(ServiceMethodPrefix + methodName);

        Assert.Equal(TimeSpan.FromSeconds(30), deadline);
    }

    [Fact]
    public void UnknownMethodUsesDefaultDeadline()
    {
        var policy = CreatePolicy();

        Assert.Equal(TimeSpan.FromSeconds(30), policy.Resolve("/other.Service/Unrelated"));
    }

    [Fact]
    public void UsesConfiguredDeadlines()
    {
        var policy = CreatePolicy(new RecommendationOptions
        {
            DeadlineSeconds = 20,
            PlanDeadlineSeconds = 45,
        });

        Assert.Equal(
            TimeSpan.FromSeconds(20),
            policy.Resolve(ServiceMethodPrefix + "GetMealRecommendations"));
        Assert.Equal(
            TimeSpan.FromSeconds(45),
            policy.Resolve(ServiceMethodPrefix + "GenerateMealPlan"));
    }

    [Theory]
    [InlineData(4, false)]
    [InlineData(5, true)]
    [InlineData(90, true)]
    [InlineData(120, true)]
    [InlineData(121, false)]
    public void DeadlineOptionMustBeBetweenFiveAnd120Seconds(int seconds, bool expected)
    {
        Assert.Equal(expected, RecommendationOptions.IsValidDeadlineSeconds(seconds));
    }

    [Fact]
    public void DefaultsAre90SecondsForPlansAnd30SecondsForExistingRpcs()
    {
        var options = new RecommendationOptions();

        Assert.Equal(90, options.PlanDeadlineSeconds);
        Assert.Equal(30, options.DeadlineSeconds);
    }

    private static RpcDeadlinePolicy CreatePolicy(RecommendationOptions? options = null) =>
        new(Options.Create(options ?? new RecommendationOptions()));
}
