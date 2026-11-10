using Recommendation.Domain.Constants;

namespace Recommendation.Application.Features.WorkoutPlans;

/// <summary>
/// Ưu tiên hoạt động theo mục tiêu tập luyện. Chỉ là gợi ý cho skeleton/prompt;
/// model vẫn chọn index trong catalog và validator kiểm tra lại.
/// </summary>
public sealed record WorkoutGoalGuidance(
    string VietnamesePhrase,
    IReadOnlyList<string> PreferredKeys,
    string Instruction)
{
    /// <summary>Guidance theo goal canonical từ gRPC.</summary>
    public static WorkoutGoalGuidance For(string? goal) => goal switch
    {
        "LOSE_WEIGHT" => new(
            "giảm cân, tăng vận động",
            ["walking", "cycling", "running", "swimming", "stairs"],
            "Ưu tiên cardio (đi bộ, đạp xe, chạy, bơi, leo cầu thang); có thể thêm 1 buổi tập tạ để giữ cơ."),
        "BUILD_MUSCLE" => new(
            "tăng cơ, sức mạnh",
            ["strength", "walking", "cycling"],
            "Bắt buộc có bài tập tạ/chống đẩy (strength) trong tuần; cardio chỉ là bài phụ hồi phục."),
        "MAINTAIN" => new(
            "duy trì sức khỏe",
            ["walking", "strength", "cycling", "swimming", "tai_chi"],
            "Trộn cardio, tập tạ và vận động nhẹ; mỗi buổi nên có 1 bài chính và 1 bài phụ."),
        "LONGEVITY" => new(
            "trường thọ, vận động nhẹ nhàng",
            ["walking", "tai_chi", "gardening", "strength", "cycling"],
            "Ưu tiên vận động nhẹ nhàng, linh hoạt; thêm 1 buổi tập tạ nhẹ nếu phù hợp."),
        _ => new(
            "vận động đều đặn",
            ["walking", "strength", "cycling", "tai_chi"],
            "Chọn hoạt động an toàn, đa dạng và phù hợp thể trạng."),
    };

    /// <summary>Sắp xếp catalog: hoạt động ưu tiên lên trước, giữ thứ tự còn lại.</summary>
    public IReadOnlyList<WorkoutActivity> Prioritize(IReadOnlyList<WorkoutActivity> activities)
    {
        ArgumentNullException.ThrowIfNull(activities);
        var preferred = PreferredKeys
            .SelectMany(key => activities.Where(activity =>
                string.Equals(activity.Key, key, StringComparison.Ordinal)))
            .Distinct()
            .ToList();
        preferred.AddRange(activities.Except(preferred));
        return preferred;
    }
}
