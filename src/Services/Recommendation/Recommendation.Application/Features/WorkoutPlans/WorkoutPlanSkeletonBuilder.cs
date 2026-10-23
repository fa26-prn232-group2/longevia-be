using Recommendation.Application.Common.Errors;
using Recommendation.Application.Features.Plans;
using Recommendation.Domain.Constants;
using Recommendation.Domain.Models;

namespace Recommendation.Application.Features.WorkoutPlans;

/// <summary>
/// Rải các buổi tập một cách xác định lên các ngày của tuần (FR-026/FR-028,
/// D-09): sau khi áp mặc định <c>min(3, days)</c>, buổi thứ <c>i</c> rơi vào ngày
/// <c>floor(i × days / sessions)</c>, mỗi ngày tối đa một buổi. Không đủ hoạt động
/// an toàn thì ngày đó là ngày nghỉ kèm <c>no_safe_activity</c>.
/// </summary>
public sealed class WorkoutPlanSkeletonBuilder(
    PlanRequestValidator requestValidator,
    PlanDateRange dateRange)
{
    /// <summary>Dựng skeleton cho một lời gọi workout plan.</summary>
    public WorkoutPlanSkeleton Build(
        GenerateWorkoutPlanRequest request,
        WorkoutActivityCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(catalog);

        var sessions = requestValidator.ResolveSessionsPerWeek(request.SessionsPerWeek, request.Days);
        if (sessions > request.Days)
        {
            throw RecommendationErrors.InvalidWorkoutParams();
        }

        var minutes = requestValidator.ResolveMinutesPerSession(request.MinutesPerSession);
        var dates = dateRange.Build(request.WeekStartDate, request.Days);

        var sessionIndexes = new HashSet<int>(sessions);
        for (var index = 0; index < sessions; index++)
        {
            sessionIndexes.Add((int)Math.Floor(index * (double)request.Days / sessions));
        }

        var warnings = new List<PlanWarning>(sessions);
        var days = new List<WorkoutPlanSkeletonDay>(dates.Count);
        var ordinal = 0;

        foreach (var date in dates)
        {
            var index = days.Count;
            if (!sessionIndexes.Contains(index))
            {
                days.Add(new WorkoutPlanSkeletonDay { Date = date });
                continue;
            }

            var activity = catalog.IsEmpty ? null : catalog.Activities[ordinal % catalog.Activities.Count];
            ordinal++;

            if (activity is null)
            {
                warnings.Add(new PlanWarning
                {
                    Code = PlanWarningCodes.NoSafeActivity,
                    Message = "Không còn hoạt động an toàn trong chương 5 theo giới hạn đã khai nên ngày này là ngày nghỉ.",
                    Scope = date,
                });
            }

            days.Add(new WorkoutPlanSkeletonDay { Date = date, Activity = activity });
        }

        return new WorkoutPlanSkeleton
        {
            Days = days,
            SessionsRequested = sessions,
            MinutesPerSession = minutes,
            Warnings = warnings,
        };
    }
}

/// <summary>Khung kế hoạch tập luyện xác định trước khi gọi mô hình.</summary>
public sealed record WorkoutPlanSkeleton
{
    /// <summary>Đúng số ngày của request, theo thứ tự ngày liên tiếp.</summary>
    public required IReadOnlyList<WorkoutPlanSkeletonDay> Days { get; init; }

    /// <summary>Số buổi sau khi áp mặc định và kiểm tra ≤ số ngày.</summary>
    public int SessionsRequested { get; init; }

    /// <summary>Thời lượng mỗi buổi sau khi áp mặc định 30 phút.</summary>
    public int MinutesPerSession { get; init; }

    /// <summary>Warning của skeleton (ngày nghỉ do thiếu hoạt động an toàn).</summary>
    public required IReadOnlyList<PlanWarning> Warnings { get; init; }
}

/// <summary>Một ngày của skeleton: có hoạt động (buổi tập) hoặc không (ngày nghỉ).</summary>
public sealed record WorkoutPlanSkeletonDay
{
    /// <summary>Ngày ISO <c>YYYY-MM-DD</c>.</summary>
    public required string Date { get; init; }

    /// <summary>Hoạt động dự phòng xác định; <c>null</c> nghĩa là ngày nghỉ.</summary>
    public WorkoutActivity? Activity { get; init; }
}
