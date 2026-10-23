using Recommendation.Domain.Models;

namespace Recommendation.Application.Features.NutrientGaps;

/// <summary>
/// Đầu vào của use case phân tích thiếu hụt vi chất (Application DTO). Contract
/// gRPC được map sang kiểu này ở tầng Api; Application không tham chiếu generated
/// code.
/// </summary>
public sealed record NutrientGapRequest
{
    /// <summary>Định danh người gọi — chỉ tồn tại trong luồng gọi, không được lưu.</summary>
    public string UserId { get; init; } = string.Empty;

    /// <summary>Hồ sơ người dùng của lời gọi này — không được lưu lại.</summary>
    public UserProfile Profile { get; init; } = new();

    /// <summary>Dữ liệu dinh dưỡng các ngày gần nhất (tối đa 7 ngày).</summary>
    public IReadOnlyList<DailyNutrientSummary> Past7Days { get; init; } = [];

    /// <summary>Mục tiêu dinh dưỡng bên gọi đặt; <c>0</c> = dùng nhu cầu tham chiếu.</summary>
    public NutrientTargets Targets { get; init; } = new();
}
