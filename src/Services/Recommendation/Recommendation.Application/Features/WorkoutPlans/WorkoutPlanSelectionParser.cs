using System.Text.Json.Nodes;
using Recommendation.Application.Features.Ask;
using Recommendation.Application.Features.Plans;
using Recommendation.Domain.Models;

namespace Recommendation.Application.Features.WorkoutPlans;

/// <summary>
/// Đọc payload <c>WorkoutPlanSelection</c> của mô hình (D-06/D-09/FR-034): chỉ
/// đọc <c>activity_index</c> và citation cho từng buổi; thời lượng, cường độ và
/// rationale do code quyết định. Ngày thiếu hoặc chỉ số sai được trả ở trạng thái
/// không hợp lệ để <see cref="WorkoutPlanValidator"/> từ chối và use case chuyển
/// sang skeleton.
/// </summary>
public sealed class WorkoutPlanSelectionParser(
    CitationResolver citationResolver,
    PlanRationaleBuilder rationaleBuilder)
{
    /// <summary>Đọc lựa chọn hoạt động; ngày nghỉ của skeleton giữ nguyên.</summary>
    public IReadOnlyList<WorkoutPlanDay> Parse(
        JsonObject? payload,
        WorkoutPlanSkeleton skeleton,
        WorkoutActivityCatalog catalog,
        IReadOnlyList<RetrievedChunk> chunks,
        IReadOnlyList<string> appliedLimitations)
    {
        ArgumentNullException.ThrowIfNull(appliedLimitations);
        ArgumentNullException.ThrowIfNull(skeleton);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(chunks);

        var days = new List<WorkoutPlanDay>(skeleton.Days.Count);

        foreach (var skeletonDay in skeleton.Days)
        {
            if (skeletonDay.Activity is null)
            {
                days.Add(new WorkoutPlanDay
                {
                    Date = skeletonDay.Date,
                    RestDay = true,
                    Rationale = "Ngày nghỉ theo khung kế hoạch và giới hạn đã khai.",
                });

                continue;
            }

            var dayNode = FindDay(payload, skeletonDay.Date);
            var activity = dayNode is null ? null : catalog.At(ReadActivityIndex(dayNode));
            IReadOnlyList<Citation> citations = dayNode is null
                ? []
                : citationResolver.ResolveOptional(dayNode, chunks);

            days.Add(new WorkoutPlanDay
            {
                Date = skeletonDay.Date,
                RestDay = false,
                Activity = activity?.Key ?? string.Empty,
                DurationMinutes = skeleton.MinutesPerSession,
                Intensity = activity?.Intensity ?? string.Empty,
                EquipmentUsed = activity is null ? [] : activity.EquipmentUsed,
                Rationale = activity is null
                    ? "Chưa xác định được hoạt động an toàn cho buổi tập này."
                    : rationaleBuilder.BuildWorkoutRationale(
                        activity.Key,
                        skeleton.MinutesPerSession,
                        appliedLimitations,
                        citations),
                Citations = citations,
            });
        }

        return days;
    }

    private static JsonObject? FindDay(JsonObject? payload, string date)
    {
        if (payload?["days"] is not JsonArray days)
        {
            return null;
        }

        foreach (var node in days)
        {
            if (node is JsonObject day
                && day["date"] is JsonValue value
                && value.TryGetValue<string>(out var dayDate)
                && string.Equals(dayDate, date, StringComparison.Ordinal))
            {
                return day;
            }
        }

        return null;
    }

    private static int ReadActivityIndex(JsonObject dayNode) =>
        dayNode["activity_index"] is JsonValue value && value.TryGetValue<int>(out var index)
            ? index
            : -1;
}
