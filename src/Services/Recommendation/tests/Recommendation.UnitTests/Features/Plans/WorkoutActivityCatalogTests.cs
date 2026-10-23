using Recommendation.Application.Features.WorkoutPlans;
using Recommendation.Domain.Constants;
using Recommendation.Domain.Models;

namespace Recommendation.UnitTests.Features.Plans;

/// <summary>
/// T047 — D-09/FR-027..FR-033/FR-036: catalog hoạt động đóng từ chương 5, lọc
/// theo preference/thiết bị/limitations, cường độ chỉ khi có căn cứ trong chunk
/// và chế độ degraded (corpus rỗng) seed từ <see cref="WorkoutActivities"/>.
/// </summary>
public sealed class WorkoutActivityCatalogTests
{
    private readonly WorkoutActivityCatalogBuilder _builder = new(new WorkoutLimitationRules());

    [Fact]
    public void KeepsOnlyActivitiesWhoseAliasesAppearInTheChapterFiveChunk()
    {
        var result = _builder.Build(Request(), [Chunk("Đi bộ nhanh một giờ mỗi ngày.")]);

        var activity = Assert.Single(result.Catalog.Activities);
        Assert.Equal("walking", activity.Key);
        Assert.True(result.CorpusLoaded);
        Assert.DoesNotContain(result.Warnings, warning => warning.Code == PlanWarningCodes.CorpusNotLoaded);
    }

    [Fact]
    public void PreferenceRestrictsTheCatalogToTheMatchingActivity()
    {
        var request = Request() with { ActivityPreference = ["walking"] };

        var result = _builder.Build(
            request,
            [Chunk("Đi bộ và đạp xe đều tốt cho sức khỏe.")]);

        var activity = Assert.Single(result.Catalog.Activities);
        Assert.Equal("walking", activity.Key);
    }

    [Fact]
    public void UnknownPreferenceYieldsEmptyCatalogWithWarning()
    {
        var request = Request() with { ActivityPreference = ["boxing"] };

        var result = _builder.Build(request, [Chunk("Đi bộ và đạp xe đều tốt.")]);

        Assert.Empty(result.Catalog.Activities);
        Assert.Contains(
            result.Warnings,
            warning => warning.Code == PlanWarningCodes.UnsupportedActivityPreference);
        Assert.DoesNotContain(
            result.Warnings,
            warning => warning.Code == PlanWarningCodes.UnsupportedLimitation);
    }

    [Fact]
    public void ActivityThatNeedsEquipmentIsDroppedWhenEquipmentIsUnavailable()
    {
        var request = Request() with
        {
            ActivityPreference = ["cycling"],
            EquipmentAvailable = [],
        };

        var result = _builder.Build(request, [Chunk("Đạp xe giúp giảm áp lực lên khớp.")]);

        Assert.Empty(result.Catalog.Activities);
        Assert.Contains(
            result.Warnings,
            warning => warning.Code == PlanWarningCodes.UnsupportedActivityPreference);
    }

    [Fact]
    public void ActivityIsKeptWhenCallerHasTheRequiredEquipment()
    {
        var request = Request() with
        {
            ActivityPreference = ["cycling"],
            EquipmentAvailable = ["bike"],
        };

        var result = _builder.Build(request, [Chunk("Đạp xe giúp giảm áp lực lên khớp.")]);

        var activity = Assert.Single(result.Catalog.Activities);
        Assert.Equal("cycling", activity.Key);
        Assert.Equal(new[] { "bike" }, activity.EquipmentUsed);
    }

    [Fact]
    public void LimitationsRemoveTheConflictingActivity()
    {
        var request = Request() with { Limitations = ["đau khớp gối"] };

        var result = _builder.Build(
            request,
            [Chunk("Chạy bộ và đi bộ đều là hoạt động thể chất.")]);

        Assert.DoesNotContain(result.Catalog.Activities, activity => activity.Key == "running");
        Assert.Contains(result.Catalog.Activities, activity => activity.Key == "walking");
        Assert.DoesNotContain(
            result.Warnings,
            warning => warning.Code == PlanWarningCodes.UnsupportedLimitation);
    }

    [Fact]
    public void UnsupportedLimitationIsIgnoredAndReported()
    {
        var request = Request() with { Limitations = ["bệnh tim bẩm sinh"] };

        var result = _builder.Build(request, [Chunk("Đi bộ nhanh mỗi ngày.")]);

        Assert.Contains(result.Catalog.Activities, activity => activity.Key == "walking");
        Assert.Contains(
            result.Warnings,
            warning => warning.Code == PlanWarningCodes.UnsupportedLimitation);
        Assert.Empty(result.AppliedLimitations);
    }

    [Fact]
    public void IntensityIsKeptOnlyWhenTheCorpusSupportsIt()
    {
        var supported = _builder.Build(
            Request(),
            [Chunk("Đi bộ nhanh một giờ mỗi ngày giúp tim mạch khỏe.")]);

        var walkingWithEvidence = Assert.Single(supported.Catalog.Activities);
        Assert.False(string.IsNullOrWhiteSpace(walkingWithEvidence.Intensity));

        var unsupported = _builder.Build(
            Request(),
            [Chunk("Đi bộ là hoạt động nhẹ nhàng phù hợp mọi người.")]);

        var walkingWithoutEvidence = Assert.Single(unsupported.Catalog.Activities);
        Assert.True(string.IsNullOrEmpty(walkingWithoutEvidence.Intensity));
    }

    [Fact]
    public void EmptyCorpusSeedsTheCatalogFromCallerPreferenceOnly()
    {
        var request = Request() with { ActivityPreference = ["walking"] };

        var result = _builder.Build(request, []);

        Assert.False(result.CorpusLoaded);
        var activity = Assert.Single(result.Catalog.Activities);
        Assert.Equal("walking", activity.Key);
        Assert.True(string.IsNullOrEmpty(activity.Intensity));
        Assert.Null(activity.Grounding);
        Assert.Contains(
            result.Warnings,
            warning => warning.Code == PlanWarningCodes.ActivitySourceUnverified);
    }

    [Fact]
    public void EmptyCorpusWithoutPreferenceYieldsEmptyCatalog()
    {
        var result = _builder.Build(Request(), []);

        Assert.False(result.CorpusLoaded);
        Assert.Empty(result.Catalog.Activities);
        Assert.DoesNotContain(
            result.Warnings,
            warning => warning.Code == PlanWarningCodes.ActivitySourceUnverified);
    }

    private static GenerateWorkoutPlanRequest Request() => new()
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
        Days = 7,
        SessionsPerWeek = 3,
        MinutesPerSession = 30,
        ActivityPreference = [],
        EquipmentAvailable = [],
        Limitations = [],
        Locale = "vi-VN",
    };

    private static RetrievedChunk Chunk(string content) => new()
    {
        Chunk = new KnowledgeChunk
        {
            CorpusId = Guid.NewGuid(),
            ChapterNumber = WorkoutActivities.SourceChapterNumber,
            ChapterTitle = "TẬP THỂ DỤC VÀ SỐNG LÂU KHỎE MẠNH",
            Content = content,
            BookPageFrom = 105,
            BookPageTo = 105,
            PdfPageFrom = 98,
            PdfPageTo = 98,
            Verified = true,
            SourceFile = "05-chuong-5-tap-the-duc-va-song-lau-khoe-manh.md",
        },
        Score = 0.9,
    };
}
