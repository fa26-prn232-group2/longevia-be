using Recommendation.Domain.Models;

namespace Recommendation.Application.Features.LongevityHabits;

/// <summary>
/// Đầu vào của use case đánh giá thói quen trường thọ (Application DTO). Contract
/// gRPC được map sang kiểu này ở tầng Api; Application không tham chiếu generated
/// code.
/// </summary>
public sealed record LongevityHabitRequest
{
    /// <summary>Định danh người gọi — chỉ tồn tại trong luồng gọi, không được lưu.</summary>
    public string UserId { get; init; } = string.Empty;

    /// <summary>Hồ sơ người dùng của lời gọi này — không được lưu lại.</summary>
    public UserProfile Profile { get; init; } = new();

    /// <summary>Thời gian nhịn ăn trung bình mỗi ngày (giờ).</summary>
    public double AverageFastingHours { get; init; }

    /// <summary>Thời gian ngủ trung bình mỗi ngày (giờ).</summary>
    public double AverageSleepHours { get; init; }

    /// <summary>Số ngày đạt mục tiêu nước trong 7 ngày gần nhất.</summary>
    public int WaterTargetHitDays { get; init; }

    /// <summary>Tỷ lệ đạt mục tiêu dinh dưỡng trong 7 ngày (%).</summary>
    public double NutritionAdherenceRate { get; init; }
}
