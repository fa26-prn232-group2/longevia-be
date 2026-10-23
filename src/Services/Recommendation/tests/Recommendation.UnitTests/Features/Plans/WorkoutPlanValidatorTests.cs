using Recommendation.Application.Features.Plans;
using Recommendation.Application.Features.WorkoutPlans;
using Recommendation.Domain.Models;

namespace Recommendation.UnitTests.Features.Plans;

/// <summary>
/// T049 — FR-027/FR-034: activity phải thuộc catalog, duration đúng caller,
/// citation verify bắt buộc khi corpus có (trừ degraded) và các bất biến của
/// ngày nghỉ.
/// </summary>
public sealed class WorkoutPlanValidatorTests
{
    private const string SessionDate = "2026-10-26";

    private readonly WorkoutPlanValidator _validator = new();

    [Fact]
    public void AcceptsAValidSessionDay()
    {
        var context = BuildContext();

        var result = _validator.Validate(
            context.Request,
            context.Skeleton,
            context.Catalog,
            [Session()],
            corpusLoaded: true);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void RejectsActivityOutsideTheCatalog()
    {
        var context = BuildContext();

        var result = _validator.Validate(
            context.Request,
            context.Skeleton,
            context.Catalog,
            [Session() with { Activity = "boxing" }],
            corpusLoaded: true);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void RejectsDurationThatDiffersFromCallerValue()
    {
        var context = BuildContext();

        var result = _validator.Validate(
            context.Request,
            context.Skeleton,
            context.Catalog,
            [Session() with { DurationMinutes = 45 }],
            corpusLoaded: true);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void RequiresVerifiedCitationWhenCorpusIsLoaded()
    {
        var context = BuildContext();

        var result = _validator.Validate(
            context.Request,
            context.Skeleton,
            context.Catalog,
            [Session() with { Citations = [] }],
            corpusLoaded: true);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void AllowsEmptyCitationInDegradedMode()
    {
        var context = BuildContext();

        var result = _validator.Validate(
            context.Request,
            context.Skeleton,
            context.Catalog,
            [Session() with { Citations = [] }],
            corpusLoaded: false);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void RejectsRestDayThatCarriesAnActivity()
    {
        var context = BuildContext(days: 2, sessions: 1);

        var result = _validator.Validate(
            context.Request,
            context.Skeleton,
            context.Catalog,
            [
                Session(),
                new WorkoutPlanDay
                {
                    Date = "2026-10-27",
                    RestDay = true,
                    Activity = "walking",
                },
            ],
            corpusLoaded: true);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void RejectsRestDayWithDurationOrCitation()
    {
        var context = BuildContext(days: 2, sessions: 1);

        var withDuration = _validator.Validate(
            context.Request,
            context.Skeleton,
            context.Catalog,
            [
                Session(),
                new WorkoutPlanDay { Date = "2026-10-27", RestDay = true, DurationMinutes = 30 },
            ],
            corpusLoaded: true);

        Assert.False(withDuration.IsValid);

        var withCitation = _validator.Validate(
            context.Request,
            context.Skeleton,
            context.Catalog,
            [
                Session(),
                new WorkoutPlanDay
                {
                    Date = "2026-10-27",
                    RestDay = true,
                    Citations = [Citation()],
                },
            ],
            corpusLoaded: true);

        Assert.False(withCitation.IsValid);
    }

    [Fact]
    public void AcceptsValidRestDay()
    {
        var context = BuildContext(days: 2, sessions: 1);

        var result = _validator.Validate(
            context.Request,
            context.Skeleton,
            context.Catalog,
            [
                Session(),
                new WorkoutPlanDay { Date = "2026-10-27", RestDay = true },
            ],
            corpusLoaded: true);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void RejectsWhenASkeletonSessionDayIsReturnedAsRest()
    {
        var context = BuildContext();

        var result = _validator.Validate(
            context.Request,
            context.Skeleton,
            context.Catalog,
            [new WorkoutPlanDay { Date = SessionDate, RestDay = true }],
            corpusLoaded: true);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void RejectsMissingDays()
    {
        var context = BuildContext(days: 2, sessions: 1);

        var result = _validator.Validate(
            context.Request,
            context.Skeleton,
            context.Catalog,
            [Session()],
            corpusLoaded: true);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void RejectsEquipmentThatIsNotPartOfTheActivity()
    {
        var context = BuildContext();

        var result = _validator.Validate(
            context.Request,
            context.Skeleton,
            context.Catalog,
            [Session() with { EquipmentUsed = ["bike"] }],
            corpusLoaded: true);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void AcceptsDeclaredIntensityAndRejectsInventedOne()
    {
        var context = BuildContext();

        Assert.True(_validator.Validate(
            context.Request,
            context.Skeleton,
            context.Catalog,
            [Session() with { Intensity = "Trung bình (3-6 MET)" }],
            corpusLoaded: true).IsValid);

        Assert.False(_validator.Validate(
            context.Request,
            context.Skeleton,
            context.Catalog,
            [Session() with { Intensity = "Rất cao" }],
            corpusLoaded: true).IsValid);
    }

    private static ValidatorContext BuildContext(int days = 1, int sessions = 1)
    {
        var request = new GenerateWorkoutPlanRequest
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
            MinutesPerSession = 30,
            ActivityPreference = ["walking"],
            EquipmentAvailable = [],
            Limitations = [],
            Locale = "vi-VN",
        };

        var catalog = new WorkoutActivityCatalog
        {
            Activities =
            [
                new WorkoutActivity
                {
                    Key = "walking",
                    DisplayName = "Đi bộ",
                    Intensity = "Trung bình (3-6 MET)",
                },
            ],
        };

        var skeleton = new WorkoutPlanSkeletonBuilder(new PlanRequestValidator(), new PlanDateRange())
            .Build(request, catalog);

        return new ValidatorContext(request, skeleton, catalog);
    }

    private static WorkoutPlanDay Session() => new()
    {
        Date = SessionDate,
        RestDay = false,
        Activity = "walking",
        DurationMinutes = 30,
        Intensity = "Trung bình (3-6 MET)",
        EquipmentUsed = [],
        Rationale = "Buổi tập theo hướng dẫn chung.",
        Citations = [Citation()],
    };

    private static Citation Citation() => new()
    {
        ChapterNumber = 5,
        ChapterTitle = "TẬP THỂ DỤC VÀ SỐNG LÂU KHỎE MẠNH",
        BookPage = 105,
        PdfPage = 98,
        Quote = "Đi bộ nhanh",
        Verified = true,
        SourceFile = "05-chuong-5-tap-the-duc-va-song-lau-khoe-manh.md",
    };

    private sealed record ValidatorContext(
        GenerateWorkoutPlanRequest Request,
        WorkoutPlanSkeleton Skeleton,
        WorkoutActivityCatalog Catalog);
}
