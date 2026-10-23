using System.Globalization;
using Recommendation.Domain.Constants;
using Recommendation.Domain.Models;

namespace Recommendation.Application.Features.WorkoutPlans;

/// <summary>
/// Kiểm tra kết quả AI của <c>GenerateWorkoutPlan</c> (FR-027/FR-028/FR-034):
/// activity phải thuộc catalog, thời lượng đúng caller, citation verify bắt buộc
/// khi corpus có dữ liệu (trừ degraded), và mọi bất biến của ngày nghỉ. Kết quả
/// không hợp lệ buộc use case chuyển sang skeleton xác định.
/// </summary>
public sealed class WorkoutPlanValidator
{
    /// <summary>Kiểm tra toàn bộ các ngày do parser dựng.</summary>
    public WorkoutPlanValidationResult Validate(
        GenerateWorkoutPlanRequest request,
        WorkoutPlanSkeleton skeleton,
        WorkoutActivityCatalog catalog,
        IReadOnlyList<WorkoutPlanDay> days,
        bool corpusLoaded)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(skeleton);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(days);

        if (days.Count != skeleton.Days.Count)
        {
            return Invalid("Số ngày của kế hoạch không khớp yêu cầu.");
        }

        for (var index = 0; index < days.Count; index++)
        {
            if (!string.Equals(days[index].Date, skeleton.Days[index].Date, StringComparison.Ordinal))
            {
                return Invalid("Thứ tự hoặc giá trị ngày của kế hoạch không khớp yêu cầu.");
            }
        }

        var expectedSessions = skeleton.Days.Count(day => day.Activity is not null);
        var actualSessions = days.Count(day => !day.RestDay);
        if (expectedSessions != actualSessions)
        {
            return Invalid("Số buổi tập không khớp skeleton.");
        }

        for (var index = 0; index < days.Count; index++)
        {
            var day = days[index];
            var skeletonDay = skeleton.Days[index];

            if (skeletonDay.Activity is null)
            {
                var restInvalid = ValidateRestDay(day);
                if (restInvalid is not null)
                {
                    return restInvalid;
                }

                continue;
            }

            if (day.RestDay)
            {
                return Invalid(string.Create(
                    CultureInfo.InvariantCulture,
                    $"Ngày {day.Date} phải là buổi tập theo skeleton nhưng lại là ngày nghỉ."));
            }

            var activity = catalog.Find(day.Activity);
            if (activity is null)
            {
                return Invalid(string.Create(
                    CultureInfo.InvariantCulture,
                    $"Hoạt động '{day.Activity}' không thuộc catalog của chương 5."));
            }

            if (day.DurationMinutes != skeleton.MinutesPerSession)
            {
                return Invalid(string.Create(
                    CultureInfo.InvariantCulture,
                    $"Thời lượng ngày {day.Date} phải bằng minutes_per_session."));
            }

            if (corpusLoaded && day.Citations.Count == 0)
            {
                return Invalid(string.Create(
                    CultureInfo.InvariantCulture,
                    $"Ngày {day.Date} thiếu citation đã verify từ chương 5."));
            }

            if (!corpusLoaded && day.Citations.Count > 0)
            {
                return Invalid(string.Create(
                    CultureInfo.InvariantCulture,
                    $"Ngày {day.Date} không được có citation khi corpus rỗng."));
            }

            if (day.Citations.Any(
                citation => citation.ChapterNumber != WorkoutActivities.SourceChapterNumber))
            {
                return Invalid(string.Create(
                    CultureInfo.InvariantCulture,
                    $"Ngày {day.Date} có citation ngoài chương 5."));
            }

            if (day.EquipmentUsed.Any(
                equipment => !activity.EquipmentUsed.Contains(equipment, StringComparer.Ordinal)))
            {
                return Invalid(string.Create(
                    CultureInfo.InvariantCulture,
                    $"Ngày {day.Date} dùng thiết bị ngoài catalog."));
            }

            if (day.Intensity.Length > 0
                && !string.Equals(day.Intensity, activity.Intensity, StringComparison.Ordinal))
            {
                return Invalid(string.Create(
                    CultureInfo.InvariantCulture,
                    $"Ngày {day.Date} có cường độ không có căn cứ trong chương 5."));
            }
        }

        return WorkoutPlanValidationResult.Valid();
    }

    private static WorkoutPlanValidationResult? ValidateRestDay(WorkoutPlanDay day)
    {
        if (!day.RestDay)
        {
            return Invalid(string.Create(
                CultureInfo.InvariantCulture,
                $"Ngày {day.Date} phải là ngày nghỉ theo skeleton."));
        }

        if (day.Activity.Length > 0
            || day.DurationMinutes != 0
            || day.EquipmentUsed.Count > 0
            || day.Citations.Count > 0
            || day.Intensity.Length > 0)
        {
            return Invalid(string.Create(
                CultureInfo.InvariantCulture,
                $"Ngày nghỉ {day.Date} vi phạm bất biến rest_day."));
        }

        return null;
    }

    private static WorkoutPlanValidationResult Invalid(string reason) =>
        new() { IsValid = false, FailureReason = reason };
}

/// <summary>Kết quả kiểm tra kế hoạch tập luyện do AI trả về.</summary>
public sealed record WorkoutPlanValidationResult
{
    /// <summary>Kế hoạch đạt toàn bộ ràng buộc cấu trúc và citation.</summary>
    public required bool IsValid { get; init; }

    /// <summary>Lý do nội bộ khi không hợp lệ; không trả cho caller.</summary>
    public string? FailureReason { get; init; }

    /// <summary>Kết quả hợp lệ dùng lại cho nhiều nhánh.</summary>
    public static WorkoutPlanValidationResult Valid() => new() { IsValid = true };
}
