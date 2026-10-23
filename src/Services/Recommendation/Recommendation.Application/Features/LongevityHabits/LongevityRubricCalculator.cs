using System.Globalization;
using Recommendation.Domain.Constants;
using Recommendation.Domain.Models;

namespace Recommendation.Application.Features.LongevityHabits;

/// <summary>
/// Chấm điểm bốn nhánh thói quen theo <see cref="LongevityRubric"/> và tính điểm
/// tổng theo trọng số (FR-012/FR-013). Toàn bộ chạy bằng code, không giao cho mô
/// hình, nên cùng một đầu vào luôn cho cùng một kết quả.
/// </summary>
public sealed class LongevityRubricCalculator
{
    /// <summary>Tính điểm tổng và breakdown từ dữ liệu thói quen của một lời gọi.</summary>
    public LongevityHabitScore Calculate(LongevityHabitRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var components = new List<ScoreComponent>(4)
        {
            Component(
                LongevityRubric.FastingKey,
                LongevityRubric.FastingWeight,
                request.AverageFastingHours,
                LongevityRubric.FastingHoursForFullScore,
                "Nhịn ăn trung bình",
                "giờ"),
            Component(
                LongevityRubric.SleepKey,
                LongevityRubric.SleepWeight,
                request.AverageSleepHours,
                LongevityRubric.SleepHoursForFullScore,
                "Giấc ngủ trung bình",
                "giờ"),
            Component(
                LongevityRubric.HydrationKey,
                LongevityRubric.HydrationWeight,
                request.WaterTargetHitDays,
                LongevityRubric.HydrationDaysForFullScore,
                "Số ngày đạt mục tiêu nước",
                "ngày"),
            Component(
                LongevityRubric.NutritionKey,
                LongevityRubric.NutritionWeight,
                request.NutritionAdherenceRate,
                LongevityRubric.NutritionAdherencePercentForFullScore,
                "Mức đạt mục tiêu dinh dưỡng",
                "%"),
        };

        var weighted = components.Sum(component => component.Score * component.Weight)
            / (double)LongevityRubric.TotalWeight;

        return new LongevityHabitScore
        {
            Score = Math.Clamp(
                (int)Math.Round(weighted, MidpointRounding.AwayFromZero),
                LongevityRubric.MinScore,
                LongevityRubric.MaxScore),
            Components = components,
        };
    }

    private static ScoreComponent Component(
        string key,
        int weight,
        double value,
        double threshold,
        string label,
        string unit)
    {
        var score = ScoreFor(value, threshold);

        return new ScoreComponent
        {
            Key = key,
            Score = score,
            Weight = weight,
            Reason = BuildReason(label, value, unit, threshold, score),
        };
    }

    /// <summary>
    /// Nội suy tuyến tính dưới ngưỡng và làm tròn xuống, nên mọi giá trị dưới
    /// ngưỡng đều cho điểm nhỏ hơn 100; giá trị âm hoặc không hữu hạn chặn về 0.
    /// </summary>
    private static int ScoreFor(double value, double threshold)
    {
        if (!double.IsFinite(value) || value <= 0)
        {
            return LongevityRubric.MinScore;
        }

        if (value >= threshold)
        {
            return LongevityRubric.MaxScore;
        }

        var scaled = Math.Floor(value / threshold * LongevityRubric.ScoreScale);
        return Math.Clamp((int)scaled, LongevityRubric.MinScore, LongevityRubric.MaxScore);
    }

    private static string BuildReason(string label, double value, string unit, double threshold, int score)
    {
        var valueText = Quantity(value, unit);
        var thresholdText = Quantity(threshold, unit);

        if (value >= threshold)
        {
            return string.Create(
                CultureInfo.InvariantCulture,
                $"{label} {valueText} đạt ngưỡng tối đa {thresholdText} nên nhánh đạt 100/100 điểm.");
        }

        if (!double.IsFinite(value) || value <= 0)
        {
            return $"{label} chưa có dữ liệu hoặc bằng 0 {unit} nên nhánh đạt 0/100 điểm.";
        }

        return string.Create(
            CultureInfo.InvariantCulture,
            $"{label} {valueText} dưới ngưỡng {thresholdText} nên nhánh đạt {score}/100 điểm.");
    }

    private static string Quantity(double value, string unit)
    {
        var formatted = value.ToString("0.##", CultureInfo.InvariantCulture);
        return unit == "%" ? $"{formatted}%" : $"{formatted} {unit}";
    }
}
