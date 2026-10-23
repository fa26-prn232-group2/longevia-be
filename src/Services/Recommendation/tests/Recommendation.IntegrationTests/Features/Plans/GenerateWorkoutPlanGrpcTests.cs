using Grpc.Core;
using Longevia.Recommendation.Contracts.V1;
using Recommendation.IntegrationTests.Features.Meals;

namespace Recommendation.IntegrationTests.Features.Plans;

/// <summary>
/// T052 — gọi server thật qua <c>Grpc.Net.Client</c> cho <c>GenerateWorkoutPlan</c>:
/// đủ 7 ngày, đúng số buổi, ngày nghỉ, limitations, corpus rỗng (degraded), lỗi
/// nghiệp vụ, warning và metadata. Fixture của lớp này không nạp corpus nên mọi
/// response hợp lệ đều đi nhánh degraded.
/// </summary>
public sealed class GenerateWorkoutPlanGrpcTests(RecommendationApiFixture fixture)
    : IClassFixture<RecommendationApiFixture>
{
    private const string WeekStart = "2026-10-26";

    [Fact]
    public async Task ReturnsSevenDaysWithThreeSessionsAndFourRestDays()
    {
        var response = await fixture.Client.GenerateWorkoutPlanAsync(BuildRequest());

        Assert.Equal(7, response.Days.Count);
        Assert.Equal(WeekStart, response.WeekStartDate);
        Assert.Equal(7, response.DaysRequested);
        Assert.Equal(3, response.SessionsScheduled);
        Assert.Equal(30, response.MinutesPerSession);
        Assert.Equal(3, response.Days.Count(day => !day.RestDay));
        Assert.Equal(4, response.Days.Count(day => day.RestDay));
        Assert.Equal(
            new[]
            {
                "2026-10-26", "2026-10-27", "2026-10-28", "2026-10-29",
                "2026-10-30", "2026-10-31", "2026-11-01",
            },
            response.Days.Select(day => day.Date));
    }

    [Fact]
    public async Task SessionsUseOnlyTheRequestedActivityWithoutEquipment()
    {
        var response = await fixture.Client.GenerateWorkoutPlanAsync(BuildRequest());

        Assert.All(
            response.Days.Where(day => !day.RestDay),
            day =>
            {
                Assert.Equal("walking", day.Activity);
                Assert.Equal(30, day.DurationMinutes);
                Assert.Empty(day.EquipmentUsed);
            });
    }

    [Fact]
    public async Task RestDaysKeepTheirInvariants()
    {
        var response = await fixture.Client.GenerateWorkoutPlanAsync(BuildRequest());

        Assert.All(
            response.Days.Where(day => day.RestDay),
            day =>
            {
                Assert.True(string.IsNullOrEmpty(day.Activity));
                Assert.Equal(0, day.DurationMinutes);
                Assert.Empty(day.EquipmentUsed);
                Assert.Empty(day.Citations);
                Assert.True(string.IsNullOrEmpty(day.Intensity));
            });
    }

    [Fact]
    public async Task AddsNoLimitationsAssumedWarning()
    {
        var response = await fixture.Client.GenerateWorkoutPlanAsync(BuildRequest());

        Assert.Contains(response.Warnings, warning => warning.Code == "no_limitations_assumed");
    }

    [Fact]
    public async Task DegradedModeReturnsCallerDataWithWarnings()
    {
        var response = await fixture.Client.GenerateWorkoutPlanAsync(BuildRequest());

        Assert.Empty(response.Citations);
        Assert.Contains(response.Warnings, warning => warning.Code == "corpus_not_loaded");
        Assert.Contains(response.Warnings, warning => warning.Code == "activity_source_unverified");
        Assert.Contains(response.Warnings, warning => warning.Code == "intensity_unverified");
        Assert.True(response.UsedFallback);
        Assert.All(response.Days.Where(day => !day.RestDay), day => Assert.Empty(day.Citations));
    }

    [Fact]
    public async Task RejectsInvalidGoal()
    {
        var request = BuildRequest();
        request.Goal = UserGoal.GoalUnspecified;

        var exception = await Assert.ThrowsAsync<RpcException>(
            async () => await fixture.Client.GenerateWorkoutPlanAsync(request));

        Assert.Equal(StatusCode.InvalidArgument, exception.StatusCode);
        Assert.Equal("recommendation.invalid_goal", exception.Trailers.GetValue("code"));
    }

    [Fact]
    public async Task RejectsSessionsGreaterThanDays()
    {
        var request = BuildRequest();
        request.Days = 3;
        request.SessionsPerWeek = 5;

        var exception = await Assert.ThrowsAsync<RpcException>(
            async () => await fixture.Client.GenerateWorkoutPlanAsync(request));

        Assert.Equal(StatusCode.InvalidArgument, exception.StatusCode);
        Assert.Equal("recommendation.invalid_workout_params", exception.Trailers.GetValue("code"));
    }

    [Fact]
    public async Task RejectsMinutesOutsideRange()
    {
        var request = BuildRequest();
        request.MinutesPerSession = 5;

        var exception = await Assert.ThrowsAsync<RpcException>(
            async () => await fixture.Client.GenerateWorkoutPlanAsync(request));

        Assert.Equal(StatusCode.InvalidArgument, exception.StatusCode);
        Assert.Equal("recommendation.invalid_workout_params", exception.Trailers.GetValue("code"));
    }

    [Fact]
    public async Task AddsUnsupportedLocaleWarning()
    {
        var request = BuildRequest();
        request.Locale = "en-US";

        var response = await fixture.Client.GenerateWorkoutPlanAsync(request);

        Assert.Contains(response.Warnings, warning => warning.Code == "unsupported_locale");
        Assert.Equal(7, response.Days.Count);
    }

    [Fact]
    public async Task AddsUnsupportedActivityPreferenceWarningAndRestDays()
    {
        var request = BuildRequest();
        request.ActivityPreference.Clear();
        request.ActivityPreference.Add("boxing");

        var response = await fixture.Client.GenerateWorkoutPlanAsync(request);

        Assert.Contains(response.Warnings, warning => warning.Code == "unsupported_activity_preference");
        Assert.Contains(response.Warnings, warning => warning.Code == "no_safe_activity");
        Assert.True(response.UsedFallback);
        Assert.Equal(0, response.SessionsScheduled);
        Assert.All(response.Days, day => Assert.True(day.RestDay));
    }

    [Fact]
    public async Task AddsUnsupportedLimitationWarning()
    {
        var request = BuildRequest();
        request.Limitations.Add("bệnh tim");

        var response = await fixture.Client.GenerateWorkoutPlanAsync(request);

        Assert.Contains(response.Warnings, warning => warning.Code == "unsupported_limitation");
        Assert.Equal(3, response.SessionsScheduled);
    }

    [Fact]
    public async Task ReturnsDisclaimerAndNoMedicalAdvice()
    {
        var response = await fixture.Client.GenerateWorkoutPlanAsync(BuildRequest());

        Assert.False(string.IsNullOrWhiteSpace(response.Disclaimer.Text));

        var text = string.Join(
            ' ',
            response.Days.Select(day => day.Rationale).Append(response.Disclaimer.Text));

        Assert.DoesNotContain("chẩn đoán", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("điều trị", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("liều thuốc", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("kê đơn", text, StringComparison.OrdinalIgnoreCase);
    }

    private static GenerateWorkoutPlanRequest BuildRequest()
    {
        var request = new GenerateWorkoutPlanRequest
        {
            Profile = new UserProfile
            {
                Age = 35,
                Sex = BiologicalSex.Female,
                HeightCm = 165,
                WeightKg = 60,
                ActivityLevel = "ModeratelyActive",
                DietaryPreference = "None",
            },
            Goal = UserGoal.Longevity,
            WeekStartDate = WeekStart,
            Days = 7,
            SessionsPerWeek = 3,
            MinutesPerSession = 30,
            Locale = "vi-VN",
        };

        request.ActivityPreference.Add("walking");

        return request;
    }
}
