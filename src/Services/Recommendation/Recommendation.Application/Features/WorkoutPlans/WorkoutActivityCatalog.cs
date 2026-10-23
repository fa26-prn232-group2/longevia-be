using Recommendation.Domain.Models;

namespace Recommendation.Application.Features.WorkoutPlans;

/// <summary>
/// Danh mục hoạt động đã được lọc cho một lời gọi workout plan (D-09): chỉ chứa
/// hoạt động có alias trong chương 5 (hoặc caller gửi ở chế độ degraded), đã qua
/// preference/thiết bị/limitations. Thứ tự phần tử là thứ tự ổn định dùng cho
/// phân bổ xác định và cho chỉ số <c>activity_index</c> gửi mô hình.
/// </summary>
public sealed record WorkoutActivityCatalog
{
    /// <summary>Danh mục rỗng: không còn hoạt động an toàn cho lời gọi.</summary>
    public static WorkoutActivityCatalog Empty { get; } = new() { Activities = [] };

    /// <summary>Các hoạt động được phép dùng, theo thứ tự ổn định.</summary>
    public IReadOnlyList<WorkoutActivity> Activities { get; init; } = [];

    /// <summary><c>true</c> khi không còn hoạt động an toàn.</summary>
    public bool IsEmpty => Activities.Count == 0;

    /// <summary>Lấy hoạt động theo chỉ số mô hình trả; <c>null</c> khi ngoài phạm vi.</summary>
    public WorkoutActivity? At(int index) =>
        index >= 0 && index < Activities.Count ? Activities[index] : null;

    /// <summary>Tìm hoạt động theo key ổn định; <c>null</c> khi không thuộc catalog.</summary>
    public WorkoutActivity? Find(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        foreach (var activity in Activities)
        {
            if (string.Equals(activity.Key, key, StringComparison.Ordinal))
            {
                return activity;
            }
        }

        return null;
    }
}

/// <summary>
/// Một hoạt động trong catalog của lời gọi: key + tên + thiết bị + cường độ + căn
/// cứ chương 5 đã verify (nếu corpus có dữ liệu).
/// </summary>
public sealed record WorkoutActivity
{
    /// <summary>Key ổn định trả cho caller.</summary>
    public required string Key { get; init; }

    /// <summary>Tên tiếng Việt theo sách.</summary>
    public required string DisplayName { get; init; }

    /// <summary>Thiết bị đã dùng; rỗng khi không cần thiết bị.</summary>
    public IReadOnlyList<string> EquipmentUsed { get; init; } = [];

    /// <summary>Cường độ có căn cứ chương 5; rỗng khi không có căn cứ.</summary>
    public string Intensity { get; init; } = string.Empty;

    /// <summary>Trích dẫn chương 5 chống đỡ hoạt động; <c>null</c> ở chế độ degraded.</summary>
    public Citation? Grounding { get; init; }
}
