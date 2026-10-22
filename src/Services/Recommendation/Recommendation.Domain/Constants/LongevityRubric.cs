namespace Recommendation.Domain.Constants;

/// <summary>
/// Rubric cố định chấm điểm thói quen trường thọ (FR-012/FR-013): trọng số công
/// khai của bốn nhánh và ngưỡng đạt điểm tối đa của từng nhánh.
/// </summary>
/// <remarks>
/// Điểm từng nhánh nội suy tuyến tính dưới ngưỡng theo công thức
/// <c>floor(clamp(value / threshold, 0, 1) * 100)</c>. Làm tròn xuống bảo đảm mọi
/// giá trị dưới ngưỡng đều cho điểm nhỏ hơn 100; giá trị âm, thiếu dữ liệu hoặc
/// không hữu hạn bị chặn về 0. Tổng điểm là trung bình có trọng số của bốn nhánh,
/// làm tròn nửa lên, nên cùng một đầu vào luôn cho cùng một điểm.
/// </remarks>
public static class LongevityRubric
{
    /// <summary>Khoá nhánh nhịn ăn gián đoạn trong <c>score_breakdown</c>.</summary>
    public const string FastingKey = "fasting";

    /// <summary>Khoá nhánh giấc ngủ trong <c>score_breakdown</c>.</summary>
    public const string SleepKey = "sleep";

    /// <summary>Khoá nhánh uống nước trong <c>score_breakdown</c>.</summary>
    public const string HydrationKey = "hydration";

    /// <summary>Khoá nhánh mức độ đạt dinh dưỡng trong <c>score_breakdown</c>.</summary>
    public const string NutritionKey = "nutrition";

    /// <summary>Trọng số phần trăm của nhánh nhịn ăn.</summary>
    public const int FastingWeight = 35;

    /// <summary>Trọng số phần trăm của nhánh giấc ngủ.</summary>
    public const int SleepWeight = 30;

    /// <summary>Trọng số phần trăm của nhánh uống nước.</summary>
    public const int HydrationWeight = 20;

    /// <summary>Trọng số phần trăm của nhánh dinh dưỡng.</summary>
    public const int NutritionWeight = 15;

    /// <summary>Tổng trọng số của bốn nhánh — luôn bằng 100.</summary>
    public const int TotalWeight = FastingWeight + SleepWeight + HydrationWeight + NutritionWeight;

    /// <summary>Nhịn ăn trung bình đạt điểm tối đa (giờ/ngày).</summary>
    public const double FastingHoursForFullScore = 16d;

    /// <summary>Giấc ngủ trung bình đạt điểm tối đa (giờ/ngày).</summary>
    public const double SleepHoursForFullScore = 7d;

    /// <summary>Số ngày đạt mục tiêu nước trong 7 ngày để đạt điểm tối đa.</summary>
    public const int HydrationDaysForFullScore = 7;

    /// <summary>Tỷ lệ đạt mục tiêu dinh dưỡng để đạt điểm tối đa (%).</summary>
    public const double NutritionAdherencePercentForFullScore = 90d;

    /// <summary>Điểm thấp nhất của một nhánh và của điểm tổng.</summary>
    public const int MinScore = 0;

    /// <summary>Điểm cao nhất của một nhánh và của điểm tổng.</summary>
    public const int MaxScore = 100;

    /// <summary>Thang điểm dùng để quy đổi tỷ lệ đạt ngưỡng thành điểm nhánh.</summary>
    public const int ScoreScale = 100;
}
