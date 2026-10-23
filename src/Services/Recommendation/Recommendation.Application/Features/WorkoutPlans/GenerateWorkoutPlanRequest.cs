using Recommendation.Domain.Models;

namespace Recommendation.Application.Features.WorkoutPlans;

/// <summary>
/// DTO Application của <c>GenerateWorkoutPlan</c> (data-model §3/FR-040/FR-041).
/// Contract gRPC được map sang kiểu này ở tầng Api.
/// </summary>
public sealed record GenerateWorkoutPlanRequest
{
    /// <summary>Hồ sơ người dùng của lời gọi này — không được lưu lại.</summary>
    public UserProfile Profile { get; init; } = new();

    /// <summary>Mục tiêu tập luyện; khác <c>GOAL_UNSPECIFIED</c>.</summary>
    public string Goal { get; init; } = string.Empty;

    /// <summary>Ngày bắt đầu tuần ISO <c>YYYY-MM-DD</c>, phải là thứ Hai.</summary>
    public string WeekStartDate { get; init; } = string.Empty;

    /// <summary>Số ngày cần lập kế hoạch (1..7).</summary>
    public int Days { get; init; }

    /// <summary>Số buổi/tuần; <c>0</c> nghĩa là <c>min(3, days)</c>.</summary>
    public int SessionsPerWeek { get; init; }

    /// <summary>Số phút mỗi buổi; <c>0</c> nghĩa là mặc định 30.</summary>
    public int MinutesPerSession { get; init; }

    /// <summary>Hoạt động ưu tiên; rỗng nghĩa là dùng hướng dẫn chung chương 5.</summary>
    public IReadOnlyList<string> ActivityPreference { get; init; } = [];

    /// <summary>Thiết bị sẵn có; rỗng nghĩa là không có thiết bị.</summary>
    public IReadOnlyList<string> EquipmentAvailable { get; init; } = [];

    /// <summary>Giới hạn sức khỏe do caller khai; rỗng nghĩa là chưa cung cấp.</summary>
    public IReadOnlyList<string> Limitations { get; init; } = [];

    /// <summary>Ngôn ngữ mong muốn; chỉ <c>vi-VN</c> được hỗ trợ.</summary>
    public string Locale { get; init; } = string.Empty;
}
