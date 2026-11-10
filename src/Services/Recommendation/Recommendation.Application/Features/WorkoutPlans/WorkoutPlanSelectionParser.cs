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
            var selected = ReadActivities(dayNode, catalog)
                .Take(Math.Max(1, Math.Min(3, skeleton.MinutesPerSession / 5)))
                .ToList();
            IReadOnlyList<Citation> citations = dayNode is null
                ? []
                : citationResolver.ResolveOptional(dayNode, chunks);

            if (selected.Count == 0)
            {
                days.Add(new WorkoutPlanDay
                {
                    Date = skeletonDay.Date,
                    RestDay = false,
                    Activity = string.Empty,
                    DurationMinutes = skeleton.MinutesPerSession,
                    Rationale = "Chưa xác định được hoạt động an toàn cho buổi tập này.",
                    Citations = citations,
                });
                continue;
            }

            var durations = Distribute(skeleton.MinutesPerSession, selected.Count);
            var activities = selected
                .Select((activity, index) => new PlannedWorkoutActivity
                {
                    Activity = activity.Key,
                    ActivityLabel = activity.DisplayName,
                    DurationMinutes = durations[index],
                    Intensity = activity.Intensity,
                    EquipmentUsed = activity.EquipmentUsed,
                })
                .ToList();
            var primary = activities[0];
            var equipment = activities
                .SelectMany(activity => activity.EquipmentUsed)
                .Distinct(StringComparer.Ordinal)
                .ToList();

            days.Add(new WorkoutPlanDay
            {
                Date = skeletonDay.Date,
                RestDay = false,
                Activity = primary.Activity,
                DurationMinutes = skeleton.MinutesPerSession,
                Intensity = primary.Intensity,
                EquipmentUsed = equipment,
                Rationale = rationaleBuilder.BuildWorkoutRationale(
                    primary.ActivityLabel,
                    skeleton.MinutesPerSession,
                    appliedLimitations,
                    citations),
                Citations = citations,
                Activities = activities,
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

    private static IReadOnlyList<WorkoutActivity> ReadActivities(
        JsonObject? dayNode, WorkoutActivityCatalog catalog)
    {
        if (dayNode is null)
        {
            return [];
        }

        var indices = new List<int>();
        if (dayNode["activity_indices"] is JsonArray array)
        {
            foreach (var item in array)
            {
                if (item is JsonValue value && value.TryGetValue<int>(out var index))
                {
                    indices.Add(index);
                }
            }
        }
        else if (dayNode["activity_index"] is JsonValue legacy
                 && legacy.TryGetValue<int>(out var legacyIndex))
        {
            indices.Add(legacyIndex);
        }

        var selected = new List<WorkoutActivity>(3);
        foreach (var index in indices)
        {
            var activity = catalog.At(index);
            if (activity is null
                || selected.Any(item => string.Equals(item.Key, activity.Key, StringComparison.Ordinal)))
            {
                continue;
            }

            selected.Add(activity);
            if (selected.Count == 3)
            {
                break;
            }
        }

        return selected;
    }

    /// <summary>Chia thời lượng buổi cho 1–3 hoạt động, mỗi hoạt động tối thiểu 5 phút.</summary>
    private static IReadOnlyList<int> Distribute(int minutes, int count)
    {
        if (count <= 1)
        {
            return [minutes];
        }

        var ratios = count switch
        {
            2 => new[] { 0.65, 0.35 },
            _ => new[] { 0.60, 0.30, 0.10 },
        };
        var result = new int[count];
        var remaining = minutes;
        for (var index = 0; index < count - 1; index++)
        {
            var reserved = 5 * (count - index - 1);
            var duration = Math.Max(5, Math.Min(
                remaining - reserved,
                (int)Math.Round(minutes * ratios[index], MidpointRounding.AwayFromZero)));
            result[index] = duration;
            remaining -= duration;
        }

        result[^1] = remaining;
        return result;
    }
}
