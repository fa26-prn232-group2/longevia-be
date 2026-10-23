using Longevia.Shared.Errors;
using Recommendation.Application.Features.Plans;
using Recommendation.Application.Features.WorkoutPlans;
using Recommendation.Domain.Constants;
using Recommendation.Domain.Models;

namespace Recommendation.UnitTests.Features.Plans;

/// <summary>
/// T048 — FR-026/FR-028/FR-031/FR-037: skeleton rải đúng số buổi (sau mặc định
/// <c>min(3, days)</c>), mỗi ngày tối đa một buổi, sessions &gt; days lỗi và
/// không đủ hoạt động an toàn thì ngày nghỉ + <c>no_safe_activity</c>.
/// </summary>
public sealed class WorkoutPlanSkeletonBuilderTests
{
    private readonly WorkoutPlanSkeletonBuilder _builder =
        new(new PlanRequestValidator(), new PlanDateRange());

    [Fact]
    public void SpreadsTheRequestedSessionsOverTheWeek()
    {
        var skeleton = _builder.Build(Request(days: 7, sessions: 3), Catalog("walking"));

        Assert.Equal(7, skeleton.Days.Count);
        Assert.Equal(3, skeleton.SessionsRequested);
        Assert.Equal(3, skeleton.Days.Count(day => day.Activity is not null));
        Assert.Equal(
            new[] { "2026-10-26", "2026-10-28", "2026-10-30" },
            skeleton.Days.Where(day => day.Activity is not null).Select(day => day.Date));
        Assert.Empty(skeleton.Warnings);
    }

    [Fact]
    public void ZeroSessionsUseMinOfThreeAndDays()
    {
        var sevenDays = _builder.Build(Request(days: 7, sessions: 0), Catalog("walking"));
        Assert.Equal(3, sevenDays.Days.Count(day => day.Activity is not null));

        var twoDays = _builder.Build(Request(days: 2, sessions: 0), Catalog("walking"));
        Assert.Equal(2, twoDays.Days.Count(day => day.Activity is not null));
    }

    [Fact]
    public void EveryDayHasAtMostOneSession()
    {
        var skeleton = _builder.Build(Request(days: 7, sessions: 7), Catalog("walking", "cycling"));

        Assert.Equal(7, skeleton.Days.Count);
        Assert.Equal(
            skeleton.Days.Count,
            skeleton.Days.Select(day => day.Date).Distinct(StringComparer.Ordinal).Count());
        Assert.All(skeleton.Days, day => Assert.NotNull(day.Activity));
    }

    [Fact]
    public void SessionsGreaterThanDaysThrowsInvalidWorkoutParams()
    {
        var exception = Assert.Throws<AppException>(
            () => _builder.Build(Request(days: 3, sessions: 5), Catalog("walking")));

        Assert.Equal("recommendation.invalid_workout_params", exception.Code);
    }

    [Fact]
    public void EmptyCatalogKeepsTheDaysAsRestDaysWithWarning()
    {
        var skeleton = _builder.Build(Request(days: 7, sessions: 3), WorkoutActivityCatalog.Empty);

        Assert.Equal(7, skeleton.Days.Count);
        Assert.All(skeleton.Days, day => Assert.Null(day.Activity));
        Assert.Equal(3, skeleton.Warnings.Count(warning => warning.Code == PlanWarningCodes.NoSafeActivity));
        Assert.All(
            skeleton.Warnings.Where(warning => warning.Code == PlanWarningCodes.NoSafeActivity),
            warning => Assert.NotEqual(PlanWarning.PlanScope, warning.Scope));
    }

    [Fact]
    public void KeepsCallerMinutesAndDefaultsToThirty()
    {
        Assert.Equal(30, _builder.Build(Request(days: 7, sessions: 3), Catalog("walking")).MinutesPerSession);
        Assert.Equal(45, _builder.Build(Request(days: 7, sessions: 3, minutes: 45), Catalog("walking")).MinutesPerSession);
    }

    [Fact]
    public void AssignsCatalogActivitiesDeterministically()
    {
        var first = _builder.Build(Request(days: 7, sessions: 3), Catalog("walking", "cycling"));
        var second = _builder.Build(Request(days: 7, sessions: 3), Catalog("walking", "cycling"));

        Assert.Equal(
            first.Days.Select(day => day.Activity?.Key),
            second.Days.Select(day => day.Activity?.Key));
        Assert.Equal(
            new[] { "walking", "cycling", "walking" },
            first.Days
                .Where(day => day.Activity is not null)
                .Select(day => day.Activity!.Key));
    }

    private static WorkoutActivityCatalog Catalog(params string[] keys) => new()
    {
        Activities = keys
            .Select(key => new WorkoutActivity { Key = key, DisplayName = key })
            .ToList(),
    };

    private static GenerateWorkoutPlanRequest Request(int days, int sessions, int minutes = 30) => new()
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
        Goal = "LONGEVITY",
        WeekStartDate = "2026-10-26",
        Days = days,
        SessionsPerWeek = sessions,
        MinutesPerSession = minutes,
        ActivityPreference = [],
        EquipmentAvailable = [],
        Limitations = [],
        Locale = "vi-VN",
    };
}
