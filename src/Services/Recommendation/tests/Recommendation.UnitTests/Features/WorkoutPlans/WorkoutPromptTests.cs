using System.Text.Json.Nodes;
using Recommendation.Application.Features.Plans;
using Recommendation.Application.Features.WorkoutPlans;
using Recommendation.Domain.Constants;
using Recommendation.Domain.Models;

namespace Recommendation.UnitTests.Features.WorkoutPlans;

/// <summary>
/// T050 — D-06/D-09: prompt chứa skeleton, catalog đóng và chunk chương 5 trong
/// khối corpus; mô hình chỉ được chọn <c>activity_index</c> + citation.
/// </summary>
public sealed class WorkoutPromptTests
{
    private readonly WorkoutPlanPromptBuilder _builder = new(new PlanPromptFormatter());

    [Fact]
    public void PromptContainsSkeletonCatalogAndChapterFiveCorpus()
    {
        var context = BuildContext();

        var prompt = _builder.Build(context.Request, context.Skeleton, context.Catalog, context.Chunks);

        Assert.Equal(StructuredResponseKind.WorkoutPlanSelection, prompt.ResponseKind);
        Assert.Contains("activity_index", prompt.SystemInstruction, StringComparison.Ordinal);
        Assert.Contains("citation", prompt.SystemInstruction, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("không viết thời lượng", prompt.SystemInstruction, StringComparison.OrdinalIgnoreCase);

        // Fixture tất định tách prompt theo ngày nên mỗi ngày tập phải có dấu hiệu "buổi".
        foreach (var day in context.Skeleton.Days)
        {
            Assert.Contains(day.Date, prompt.UserPrompt, StringComparison.Ordinal);

            if (day.Activity is not null)
            {
                Assert.Contains($"buổi tập", prompt.UserPrompt, StringComparison.Ordinal);
            }
        }

        Assert.Contains("[0] key=walking", prompt.UserPrompt, StringComparison.Ordinal);
        Assert.Contains(PlanPromptFormatter.CorpusBeginMarker, prompt.UserPrompt, StringComparison.Ordinal);
        Assert.Contains(PlanPromptFormatter.CorpusEndMarker, prompt.UserPrompt, StringComparison.Ordinal);
        Assert.Contains("Đi bộ nhanh", prompt.UserPrompt, StringComparison.Ordinal);
    }

    [Fact]
    public void PromptDoesNotAskTheModelForNumbersOrRationale()
    {
        var context = BuildContext();

        var prompt = _builder.Build(context.Request, context.Skeleton, context.Catalog, context.Chunks);

        Assert.Contains("không viết thời lượng", prompt.SystemInstruction, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("không viết cường độ", prompt.SystemInstruction, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("không viết rationale", prompt.SystemInstruction, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PromptMarksRestDaysWithoutSessionMarker()
    {
        var context = BuildContext();

        var prompt = _builder.Build(context.Request, context.Skeleton, context.Catalog, context.Chunks);

        var restDate = context.Skeleton.Days.First(day => day.Activity is null).Date;
        var restLine = prompt.UserPrompt
            .Split('\n')
            .First(line => line.StartsWith(restDate, StringComparison.Ordinal));

        Assert.Contains("ngày nghỉ", restLine, StringComparison.Ordinal);
        Assert.DoesNotContain("buổi tập", restLine, StringComparison.Ordinal);
    }

    private static PromptContext BuildContext()
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
            Days = 7,
            SessionsPerWeek = 3,
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

        return new PromptContext(request, skeleton, catalog, [Chunk()]);
    }

    private static RetrievedChunk Chunk() => new()
    {
        Chunk = new KnowledgeChunk
        {
            CorpusId = Guid.NewGuid(),
            ChapterNumber = WorkoutActivities.SourceChapterNumber,
            ChapterTitle = "TẬP THỂ DỤC VÀ SỐNG LÂU KHỎE MẠNH",
            Content = "Đi bộ nhanh một giờ mỗi ngày.",
            BookPageFrom = 105,
            BookPageTo = 105,
            PdfPageFrom = 98,
            PdfPageTo = 98,
            Verified = true,
            SourceFile = "05-chuong-5-tap-the-duc-va-song-lau-khoe-manh.md",
        },
        Score = 0.9,
    };

    private sealed record PromptContext(
        GenerateWorkoutPlanRequest Request,
        WorkoutPlanSkeleton Skeleton,
        WorkoutActivityCatalog Catalog,
        IReadOnlyList<RetrievedChunk> Chunks);
}
