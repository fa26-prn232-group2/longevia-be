using Longevia.Recommendation.Contracts.V1;
using Recommendation.IntegrationTests.Features.Meals;
using Recommendation.IntegrationTests.Features.Shared;

namespace Recommendation.IntegrationTests.Features.NutrientGaps;

/// <summary>
/// T094 — FR-008..FR-010: host thật + PostgreSQL thật, 7 ngày thiếu chất xơ.
/// <c>gaps[]</c> phải chứa chất xơ với nhu cầu theo nhóm tuổi/giới, và
/// <c>deficient_nutrients</c> phải khớp đúng <c>gaps[]</c>.
/// </summary>
public sealed class AnalyzeNutrientGapsGrpcTests(RecommendationApiFixture fixture)
    : IClassFixture<RecommendationApiFixture>, IAsyncLifetime
{
    private static readonly SeedChunk FiberChunk = new()
    {
        Content =
            "Chất xơ giúp nuôi dưỡng hệ vi sinh đường ruột và có nhiều trong yến mạch, "
            + "đậu lăng, bông cải xanh và các loại đậu.",
        BookPageFrom = 88,
        PdfPageFrom = 80,
        Verified = true,
    };

    public Task InitializeAsync() =>
        CorpusSeeder.SeedAsync(fixture.Services, [FiberChunk], CancellationToken.None);

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task ReportsFiberGapWithTheReferenceTargetForAgeAndSex()
    {
        var response = await fixture.Client.AnalyzeNutrientGapsAsync(BuildRequest());

        var gap = Assert.Single(response.Gaps);
        Assert.Equal("Fiber", gap.Nutrient);
        Assert.Equal("g", gap.Unit);
        Assert.Equal(25, gap.Target, 3);
        Assert.Equal(10, gap.AverageIntake, 3);
        Assert.Equal(15, gap.Deficit, 3);
        Assert.Equal("severe", gap.Severity);
        Assert.Equal(gap.Nutrient, Assert.Single(response.DeficientNutrients));
        Assert.False(string.IsNullOrWhiteSpace(response.Disclaimer.Text));
        Assert.False(string.IsNullOrWhiteSpace(response.AnalysisSummary));
    }

    [Fact]
    public async Task ReportsMicronutrientGapWhenTheCallerSendsOne()
    {
        var request = BuildRequest();
        foreach (var day in request.Past7Days)
        {
            day.Micronutrients.Add(new MicronutrientIntake
            {
                Nutrient = "Vitamin D",
                Unit = "mcg",
                Amount = 5,
            });
        }

        var response = await fixture.Client.AnalyzeNutrientGapsAsync(request);

        var gap = Assert.Single(response.Gaps, item => item.Nutrient == "Vitamin D");
        Assert.Equal("µg", gap.Unit);
        Assert.Equal(15, gap.Target, 3);
        Assert.Equal(5, gap.AverageIntake, 3);
        Assert.Equal(10, gap.Deficit, 3);
        Assert.Equal("severe", gap.Severity);
        Assert.Contains("Vitamin D", response.DeficientNutrients);
    }

    private static NutrientGapRequest BuildRequest()
    {
        var request = new NutrientGapRequest
        {
            UserId = "user-integration",
            Profile = new UserProfile
            {
                Age = 30,
                Sex = BiologicalSex.Female,
            },
            TargetCalories = 2000,
            TargetProteinGrams = 46,
            TargetCarbsGrams = 130,
            TargetFatGrams = 67,
            // 0 = lấy nhu cầu tham chiếu theo nhóm tuổi/giới (nữ 19–30: 25 g).
            TargetFiberGrams = 0,
        };

        for (var day = 0; day < 7; day++)
        {
            request.Past7Days.Add(new DailyNutrientSummary
            {
                Date = $"2026-10-{15 + day}",
                Calories = 2000,
                Protein = 46,
                Carbs = 130,
                Fat = 67,
                Fiber = 10,
            });
        }

        return request;
    }
}
