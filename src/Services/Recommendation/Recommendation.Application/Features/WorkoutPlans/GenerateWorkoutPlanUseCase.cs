using Recommendation.Application.Abstractions;
using Recommendation.Application.Features.Plans;
using Recommendation.Domain.Constants;
using Recommendation.Domain.Models;

namespace Recommendation.Application.Features.WorkoutPlans;

/// <summary>
/// Pipeline của <c>GenerateWorkoutPlan</c> (FR-026..FR-037): validate → truy xuất
/// chương 5 → catalog đóng → skeleton xác định → AI chọn <c>activity_index</c> →
/// parse/validate → skeleton fallback → rationale/warnings/metadata. Service chỉ
/// trả kế hoạch, không lưu dữ liệu người dùng.
/// </summary>
public sealed class GenerateWorkoutPlanUseCase(
    PlanRequestValidator requestValidator,
    WorkoutActivityCatalogBuilder catalogBuilder,
    WorkoutPlanSkeletonBuilder skeletonBuilder,
    WorkoutPlanPromptBuilder promptBuilder,
    WorkoutPlanSelectionParser parser,
    WorkoutPlanValidator validator,
    PlanRationaleBuilder rationaleBuilder,
    IKnowledgeStore knowledgeStore,
    IChatModel chatModel)
{
    private const int ContextTopK = 8;

    /// <summary>Chạy toàn bộ pipeline cho một lời gọi.</summary>
    public async Task<GenerateWorkoutPlanResult> ExecuteAsync(
        GenerateWorkoutPlanRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        requestValidator.Validate(request);

        var warnings = new List<PlanWarning>();

        if (!PlanRequestValidator.IsSupportedLocale(request.Locale))
        {
            warnings.Add(new PlanWarning
            {
                Code = PlanWarningCodes.UnsupportedLocale,
                Message = "Locale không được hỗ trợ nên nội dung dùng tiếng Việt (vi-VN).",
            });
        }

        if (request.Limitations.Count == 0)
        {
            warnings.Add(new PlanWarning
            {
                Code = PlanWarningCodes.NoLimitationsAssumed,
                Message = "Caller chưa khai giới hạn sức khỏe nên chỉ dùng hướng dẫn chung của chương 5, không giả định khỏe mạnh hay chấn thương.",
            });
        }

        var chunks = await RetrieveAsync(request, cancellationToken).ConfigureAwait(false);
        var corpusLoaded = chunks.Count > 0;

        if (!corpusLoaded)
        {
            warnings.Add(new PlanWarning
            {
                Code = PlanWarningCodes.CorpusNotLoaded,
                Message = "Kho tri thức sách chưa có chương 5 nên citations để rỗng và chỉ dùng dữ liệu caller gửi.",
            });
        }

        var catalogResult = catalogBuilder.Build(request, chunks);
        warnings.AddRange(catalogResult.Warnings);

        var skeleton = skeletonBuilder.Build(request, catalogResult.Catalog);
        warnings.AddRange(skeleton.Warnings);

        var usedFallback = false;
        IReadOnlyList<WorkoutPlanDay> days;

        if (corpusLoaded && !catalogResult.Catalog.IsEmpty)
        {
            var selected = await TrySelectAsync(
                    request,
                    skeleton,
                    catalogResult.Catalog,
                    chunks,
                    catalogResult.AppliedLimitations,
                    cancellationToken)
                .ConfigureAwait(false);

            if (selected is null)
            {
                usedFallback = true;
                warnings.Add(new PlanWarning
                {
                    Code = PlanWarningCodes.AiFallbackUsed,
                    Message = "Không dùng được kết quả AI nên đã trả khung kế hoạch xác định từ chương 5 và giới hạn đã khai.",
                });

                days = BuildSkeletonDays(skeleton, catalogResult.AppliedLimitations);
            }
            else
            {
                days = selected;
            }
        }
        else
        {
            // Corpus rỗng hoặc không còn hoạt động an toàn: không gọi provider,
            // trả đúng khung xác định đã dựng (FR-031/FR-033).
            days = BuildSkeletonDays(skeleton, catalogResult.AppliedLimitations);
        }

        foreach (var day in days.Where(day => !day.RestDay && string.IsNullOrEmpty(day.Intensity)))
        {
            warnings.Add(new PlanWarning
            {
                Code = PlanWarningCodes.IntensityUnverified,
                Message = "Không có câu hướng dẫn cường độ trong chương 5 cho buổi tập này nên intensity để rỗng.",
                Scope = day.Date,
            });
        }

        return new GenerateWorkoutPlanResult
        {
            Days = days,
            WeekStartDate = request.WeekStartDate,
            DaysRequested = request.Days,
            SessionsScheduled = days.Count(day => !day.RestDay),
            MinutesPerSession = skeleton.MinutesPerSession,
            Citations = CollectCitations(days),
            Warnings = Deduplicate(warnings),
            Disclaimer = DisclaimerText.Value,
            UsedFallback = usedFallback,
        };
    }

    private async Task<IReadOnlyList<WorkoutPlanDay>?> TrySelectAsync(
        GenerateWorkoutPlanRequest request,
        WorkoutPlanSkeleton skeleton,
        WorkoutActivityCatalog catalog,
        IReadOnlyList<RetrievedChunk> chunks,
        IReadOnlyList<string> appliedLimitations,
        CancellationToken cancellationToken)
    {
        var prompt = promptBuilder.Build(request, skeleton, catalog, chunks);

        ChatCompletion completion;
        try
        {
            completion = await chatModel.CompleteAsync(prompt, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            // Provider lỗi/timeout: use case vẫn trả skeleton thay vì làm hỏng request.
            return null;
        }

        if (completion is not { Abstained: false, Payload: not null })
        {
            return null;
        }

        var parsed = parser.Parse(completion.Payload, skeleton, catalog, chunks, appliedLimitations);
        return validator.Validate(request, skeleton, catalog, parsed, corpusLoaded: true).IsValid
            ? parsed
            : null;
    }

    private IReadOnlyList<WorkoutPlanDay> BuildSkeletonDays(
        WorkoutPlanSkeleton skeleton,
        IReadOnlyList<string> limitations) =>
        skeleton.Days
            .Select(day => day.Activity is null
                ? new WorkoutPlanDay
                {
                    Date = day.Date,
                    RestDay = true,
                    Rationale = "Ngày nghỉ theo khung kế hoạch và giới hạn đã khai.",
                }
                : new WorkoutPlanDay
                {
                    Date = day.Date,
                    RestDay = false,
                    Activity = day.Activity.Key,
                    DurationMinutes = skeleton.MinutesPerSession,
                    Intensity = day.Activity.Intensity,
                    EquipmentUsed = day.Activity.EquipmentUsed,
                    Rationale = rationaleBuilder.BuildWorkoutRationale(
                        day.Activity.Key,
                        skeleton.MinutesPerSession,
                        limitations,
                        day.Activity.Grounding is null
                            ? Array.Empty<Citation>()
                            : new[] { day.Activity.Grounding }),
                    Citations = day.Activity.Grounding is null
                        ? Array.Empty<Citation>()
                        : new[] { day.Activity.Grounding },
                })
            .ToList();

    private async Task<IReadOnlyList<RetrievedChunk>> RetrieveAsync(
        GenerateWorkoutPlanRequest request,
        CancellationToken cancellationToken)
    {
        var query = BuildQuery(request);

        var retrieved = await knowledgeStore.SearchAsync(
                new KnowledgeQuery
                {
                    Query = query,
                    ChapterNumber = WorkoutActivities.SourceChapterNumber,
                    TopK = ContextTopK,
                },
                cancellationToken)
            .ConfigureAwait(false);

        return retrieved
            .Where(chunk => chunk.Chunk.ChapterNumber == WorkoutActivities.SourceChapterNumber)
            .ToList();
    }

    private static string BuildQuery(GenerateWorkoutPlanRequest request) =>
        string.Join(
            ' ',
            new[]
            {
                "tập luyện thể dục tuổi thọ",
                request.Goal,
                request.Profile.ActivityLevel,
                string.Join(' ', request.ActivityPreference),
                string.Join(' ', request.EquipmentAvailable),
                string.Join(' ', request.Limitations),
            }.Where(part => !string.IsNullOrWhiteSpace(part)));

    private static IReadOnlyList<Citation> CollectCitations(IReadOnlyList<WorkoutPlanDay> days) =>
        days
            .SelectMany(day => day.Citations)
            .GroupBy(citation => (citation.ChapterNumber, citation.BookPage, citation.Quote))
            .Select(group => group.First())
            .ToList();

    private static IReadOnlyList<PlanWarning> Deduplicate(List<PlanWarning> warnings) =>
        warnings
            .GroupBy(warning => (warning.Code, warning.Scope))
            .Select(group => group.First())
            .ToList();
}
