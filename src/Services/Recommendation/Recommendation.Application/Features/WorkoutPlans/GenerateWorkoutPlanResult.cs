using Recommendation.Domain.Models;

namespace Recommendation.Application.Features.WorkoutPlans;

/// <summary>
/// Kết quả use case <c>GenerateWorkoutPlan</c> (data-model §5). Chỉ là DTO nội bộ
/// tồn tại trong một lời gọi; không được lưu xuống database.
/// </summary>
public sealed record GenerateWorkoutPlanResult
{
    /// <summary>Đúng <see cref="DaysRequested"/> ngày, liên tiếp từ <see cref="WeekStartDate"/>.</summary>
    public IReadOnlyList<WorkoutPlanDay> Days { get; init; } = [];

    /// <summary>Ngày bắt đầu tuần ISO <c>YYYY-MM-DD</c>.</summary>
    public string WeekStartDate { get; init; } = string.Empty;

    /// <summary>Số ngày caller yêu cầu.</summary>
    public int DaysRequested { get; init; }

    /// <summary>Số buổi thực tế sau khi áp mặc định/giới hạn an toàn.</summary>
    public int SessionsScheduled { get; init; }

    /// <summary>Thời lượng mỗi buổi (phút).</summary>
    public int MinutesPerSession { get; init; }

    /// <summary>Hợp nhất citation đã verify; rỗng khi corpus rỗng.</summary>
    public IReadOnlyList<Citation> Citations { get; init; } = [];

    /// <summary>Cảnh báo cấp plan/ngày.</summary>
    public IReadOnlyList<PlanWarning> Warnings { get; init; } = [];

    /// <summary>Khuyến cáo BR-04 — luôn có mặt.</summary>
    public required string Disclaimer { get; init; }

    /// <summary><c>true</c> khi đã dùng skeleton/phương án dự phòng.</summary>
    public bool UsedFallback { get; init; }
}

/// <summary>Một ngày của kế hoạch tập luyện: buổi tập hoặc ngày nghỉ (data-model §5.1).</summary>
public sealed record WorkoutPlanDay
{
    /// <summary>Ngày ISO <c>YYYY-MM-DD</c>.</summary>
    public required string Date { get; init; }

    /// <summary><c>true</c> = ngày nghỉ.</summary>
    public bool RestDay { get; init; }

    /// <summary>Key hoạt động ổn định trong catalog; rỗng khi <see cref="RestDay"/>.</summary>
    public string Activity { get; init; } = string.Empty;

    /// <summary>Thời lượng buổi tập (phút); <c>0</c> khi <see cref="RestDay"/>.</summary>
    public int DurationMinutes { get; init; }

    /// <summary>Cường độ có căn cứ chương 5; rỗng khi không có căn cứ.</summary>
    public string Intensity { get; init; } = string.Empty;

    /// <summary>Thiết bị đã dùng; rỗng khi không cần.</summary>
    public IReadOnlyList<string> EquipmentUsed { get; init; } = [];

    /// <summary>Lý do do code dựng từ request + activity + citation đã verify.</summary>
    public string Rationale { get; init; } = string.Empty;

    /// <summary>Citation đã verify; rỗng khi <see cref="RestDay"/> hoặc corpus rỗng.</summary>
    public IReadOnlyList<Citation> Citations { get; init; } = [];
}
