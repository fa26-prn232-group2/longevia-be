using Recommendation.Domain.Constants;
using Recommendation.Domain.Models;

namespace Recommendation.Application.Features.NutrientGaps;

/// <summary>
/// Tính khoảng thiếu hụt của các dưỡng chất có dữ liệu nạp vào (FR-009/FR-010):
/// trung bình các ngày được gửi, nhu cầu theo nhóm tuổi + giới (hoặc mục tiêu
/// bên gọi đặt), mức độ theo biên 25% / 50%. Toàn bộ chạy bằng code.
/// </summary>
/// <remarks>
/// Dưỡng chất đa lượng (năng lượng, protein, carbohydrate, chất béo, chất xơ)
/// lấy từ trường riêng của <c>DailyNutrientSummary</c>. Vi chất lấy từ danh sách
/// <c>micronutrients</c> bên gọi gửi: chỉ những vi chất thực sự có trong dữ liệu
/// mới được chấm khoảng thiếu — không gửi nghĩa là "chưa có dữ liệu", không phải
/// "đã nạp 0".
/// </remarks>
public sealed class NutrientGapCalculator
{
    /// <summary>Mức thiếu dưới 25% nhu cầu.</summary>
    public const string Mild = "mild";

    /// <summary>Mức thiếu từ 25% đến 50% nhu cầu.</summary>
    public const string Moderate = "moderate";

    /// <summary>Mức thiếu trên 50% nhu cầu.</summary>
    public const string Severe = "severe";

    private const double MildBoundary = 0.25;
    private const double ModerateBoundary = 0.50;
    private const int RoundingDigits = 2;

    private static readonly string[] MeasuredNutrients =
    [
        "Energy",
        "Protein",
        "Carbohydrate",
        "Fat",
        "Fiber",
    ];

    /// <summary>Tính danh sách khoảng thiếu; rỗng khi không có ngày dữ liệu nào.</summary>
    public IReadOnlyList<NutrientGap> Calculate(
        UserProfile profile,
        IReadOnlyList<DailyNutrientSummary> days,
        NutrientTargets targets)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(days);
        ArgumentNullException.ThrowIfNull(targets);

        if (days.Count == 0)
        {
            return [];
        }

        var gaps = new List<NutrientGap>(MeasuredNutrients.Length);
        AddMacronutrientGaps(gaps, profile, days, targets);
        AddMicronutrientGaps(gaps, profile, days);

        return gaps
            .OrderByDescending(gap => SeverityRank(gap.Severity))
            .ThenByDescending(gap => gap.Deficit / gap.Target)
            .ThenBy(gap => gap.Nutrient, StringComparer.Ordinal)
            .ToList();
    }

    private static void AddMacronutrientGaps(
        List<NutrientGap> gaps,
        UserProfile profile,
        IReadOnlyList<DailyNutrientSummary> days,
        NutrientTargets targets)
    {
        foreach (var nutrient in MeasuredNutrients)
        {
            var target = ResolveTarget(nutrient, profile, targets);
            if (target <= 0)
            {
                continue;
            }

            var average = AverageIntake(nutrient, days);
            var deficit = target - average;
            if (Round(deficit) <= 0)
            {
                continue;
            }

            var reference = MicronutrientRda.Get(nutrient, profile.Age, profile.Sex);
            gaps.Add(new NutrientGap
            {
                Nutrient = reference.Nutrient,
                Unit = reference.Unit,
                Target = Round(target),
                AverageIntake = Round(average),
                Deficit = Round(deficit),
                Severity = SeverityFor(deficit, target),
            });
        }
    }

    /// <summary>
    /// Bổ sung khoảng thiếu cho đúng những vi chất bên gọi đã gửi. Trung bình chỉ
    /// tính trên các ngày báo cáo vi chất đó (không coi ngày vắng mặt là nạp 0);
    /// tên khớp bảng RDA sau khi bỏ khoảng trắng thừa và không phân biệt hoa
    /// thường; đơn vị không quy đổi an toàn được thì bỏ qua vi chất đó.
    /// </summary>
    private static void AddMicronutrientGaps(
        List<NutrientGap> gaps,
        UserProfile profile,
        IReadOnlyList<DailyNutrientSummary> days)
    {
        foreach (var reported in CollectMicronutrientIntakes(profile, days))
        {
            var average = reported.Total / reported.ReportedDays;
            var deficit = reported.Reference.Target - average;
            if (Round(deficit) <= 0)
            {
                continue;
            }

            gaps.Add(new NutrientGap
            {
                Nutrient = reported.Reference.Nutrient,
                Unit = reported.Reference.Unit,
                Target = Round(reported.Reference.Target),
                AverageIntake = Round(average),
                Deficit = Round(deficit),
                Severity = SeverityFor(deficit, reported.Reference.Target),
            });
        }
    }

    private static List<ReportedMicronutrient> CollectMicronutrientIntakes(
        UserProfile profile,
        IReadOnlyList<DailyNutrientSummary> days)
    {
        var collected = new List<ReportedMicronutrient>();
        foreach (var day in days)
        {
            foreach (var (reference, amount) in CollectDayIntakes(profile, day))
            {
                var index = collected.FindIndex(
                    item => string.Equals(item.Reference.Nutrient, reference.Nutrient, StringComparison.Ordinal));
                if (index < 0)
                {
                    collected.Add(new ReportedMicronutrient(reference, amount, 1));
                }
                else
                {
                    var current = collected[index];
                    collected[index] = current with
                    {
                        Total = current.Total + amount,
                        ReportedDays = current.ReportedDays + 1,
                    };
                }
            }
        }

        return collected;
    }

    /// <summary>Gộp các dòng cùng vi chất trong một ngày (ví dụ nhiều nguồn thực phẩm).</summary>
    private static List<(MicronutrientRda.Reference Reference, double Amount)> CollectDayIntakes(
        UserProfile profile,
        DailyNutrientSummary day)
    {
        var merged = new List<(MicronutrientRda.Reference Reference, double Amount)>();
        foreach (var intake in day.Micronutrients)
        {
            if (!TryResolveIntake(profile, intake, out var reference, out var amount))
            {
                continue;
            }

            var index = merged.FindIndex(
                item => string.Equals(item.Reference.Nutrient, reference!.Nutrient, StringComparison.Ordinal));
            if (index < 0)
            {
                merged.Add((reference!, amount));
            }
            else
            {
                merged[index] = (merged[index].Reference, merged[index].Amount + amount);
            }
        }

        return merged;
    }

    /// <summary>
    /// Chuẩn hoá tên vi chất và quy đổi lượng về đơn vị của bảng RDA. Trả
    /// <c>false</c> khi tên không có trong bảng, trùng dưỡng chất đa lượng đã
    /// tính từ trường riêng, lượng âm/không hữu hạn, hoặc đơn vị không quy đổi
    /// an toàn được (ví dụ IU) — bỏ qua thay vì suy đoán.
    /// </summary>
    private static bool TryResolveIntake(
        UserProfile profile,
        MicronutrientIntake intake,
        out MicronutrientRda.Reference? reference,
        out double amount)
    {
        reference = null;
        amount = 0;

        if (intake is null || !double.IsFinite(intake.Amount) || intake.Amount < 0)
        {
            return false;
        }

        var nutrient = NormalizeNutrientName(intake.Nutrient);
        if (nutrient.Length == 0
            || MeasuredNutrients.Contains(nutrient, StringComparer.OrdinalIgnoreCase)
            || !MicronutrientRda.TryGet(nutrient, profile.Age, profile.Sex, out reference)
            || !MicronutrientUnitHelper.TryConvert(intake.Amount, intake.Unit, reference!.Unit, out amount))
        {
            reference = null;
            return false;
        }

        return true;
    }

    private static string NormalizeNutrientName(string? nutrient) =>
        string.Join(
            ' ',
            (nutrient ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private static double ResolveTarget(string nutrient, UserProfile profile, NutrientTargets targets)
    {
        var requested = RequestedTarget(nutrient, targets);
        if (requested > 0)
        {
            return requested;
        }

        return MicronutrientRda.TryGet(nutrient, profile.Age, profile.Sex, out var reference)
            ? reference!.Target
            : 0;
    }

    private static double RequestedTarget(string nutrient, NutrientTargets targets) => nutrient switch
    {
        "Energy" => targets.TargetCalories,
        "Protein" => targets.TargetProteinGrams,
        "Carbohydrate" => targets.TargetCarbsGrams,
        "Fat" => targets.TargetFatGrams,
        "Fiber" => targets.TargetFiberGrams,
        _ => 0,
    };

    private static double AverageIntake(string nutrient, IReadOnlyList<DailyNutrientSummary> days)
    {
        var total = 0d;
        foreach (var day in days)
        {
            total += Intake(nutrient, day);
        }

        return total / days.Count;
    }

    private static double Intake(string nutrient, DailyNutrientSummary day) => nutrient switch
    {
        "Energy" => day.Calories,
        "Protein" => day.Protein,
        "Carbohydrate" => day.Carbs,
        "Fat" => day.Fat,
        "Fiber" => day.Fiber,
        _ => 0,
    };

    private static string SeverityFor(double deficit, double target)
    {
        var ratio = deficit / target;

        if (ratio < MildBoundary)
        {
            return Mild;
        }

        return ratio <= ModerateBoundary ? Moderate : Severe;
    }

    private static int SeverityRank(string severity) => severity switch
    {
        Severe => 3,
        Moderate => 2,
        _ => 1,
    };

    private static double Round(double value) =>
        Math.Round(value, RoundingDigits, MidpointRounding.AwayFromZero);

    /// <summary>Vi chất đã gộp qua các ngày, kèm số ngày thực sự báo cáo vi chất đó.</summary>
    private sealed record ReportedMicronutrient(
        MicronutrientRda.Reference Reference,
        double Total,
        int ReportedDays);
}
