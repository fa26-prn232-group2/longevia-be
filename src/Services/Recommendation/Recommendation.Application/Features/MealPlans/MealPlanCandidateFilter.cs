using System.Globalization;
using Recommendation.Application.Features.Meals;
using Recommendation.Domain.Constants;
using Recommendation.Domain.Models;

namespace Recommendation.Application.Features.MealPlans;

/// <summary>
/// Chuẩn bị danh mục món ứng viên trước khi gọi AI cho <c>GenerateMealPlan</c>
/// (FR-015/FR-018/FR-025): dedupe <c>food_id</c>, cắt về tối đa 120 món theo
/// macro-fit, loại allergen, loại món trái sở thích ăn uống và loại
/// <c>excluded_food_ids</c>.
/// </summary>
public sealed class MealPlanCandidateFilter(
    PlanAllergenFilter allergenFilter,
    DietaryPreferenceFilter dietaryPreferenceFilter)
{
    /// <summary>Lọc danh mục và trả kèm warning cho các bước biến đổi dữ liệu.</summary>
    public MealPlanCandidateFilterResult Filter(GenerateMealPlanRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var warnings = new List<PlanWarning>();
        var deduped = Dedupe(request.CandidateFoods, warnings);
        var trimmed = Trim(deduped, request, warnings);

        var safe = allergenFilter.Filter(trimmed, request.Profile.Allergies);
        var preferred = dietaryPreferenceFilter.Filter(safe, request.Profile.DietaryPreference);

        var excluded = request.ExcludedFoodIds is { Count: > 0 }
            ? new HashSet<string>(request.ExcludedFoodIds, StringComparer.Ordinal)
            : null;

        var candidates = excluded is null
            ? preferred.ToList()
            : preferred.Where(food => !excluded.Contains(food.FoodId)).ToList();

        return new MealPlanCandidateFilterResult
        {
            Candidates = candidates,
            Warnings = warnings,
        };
    }

    /// <summary>
    /// Điểm macro-fit càng nhỏ càng khớp: sai lệch calo so với ngân sách một bữa
    /// trung bình cộng sai lệch tỷ lệ protein/carbs/fat so với mục tiêu ngày
    /// (D-05). Mẫu số bằng 0 thì bỏ thành phần tương ứng.
    /// </summary>
    public static double MacroFitScore(CandidateFood food, GenerateMealPlanRequest request)
    {
        ArgumentNullException.ThrowIfNull(food);
        ArgumentNullException.ThrowIfNull(request);

        var score = 0d;

        var averageMealCalories = request.MealsPerDay > 0
            ? request.TargetCalories / (double)request.MealsPerDay
            : request.TargetCalories;

        if (averageMealCalories > 0)
        {
            score += Math.Abs(food.Calories - averageMealCalories) / averageMealCalories;
        }

        var targetTotal = MacroEnergy(request.TargetProteinGrams, request.TargetCarbsGrams, request.TargetFatGrams);
        var foodTotal = MacroEnergy(food.ProteinGrams, food.CarbsGrams, food.FatGrams);

        if (targetTotal > 0 && foodTotal > 0)
        {
            score += Math.Abs((food.ProteinGrams * 4 / foodTotal) - (request.TargetProteinGrams * 4 / targetTotal));
            score += Math.Abs((food.CarbsGrams * 4 / foodTotal) - (request.TargetCarbsGrams * 4 / targetTotal));
            score += Math.Abs((food.FatGrams * 9 / foodTotal) - (request.TargetFatGrams * 9 / targetTotal));
        }

        return score;
    }

    private static List<CandidateFood> Dedupe(
        IReadOnlyList<CandidateFood> candidates,
        List<PlanWarning> warnings)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var unique = new List<CandidateFood>(candidates.Count);
        var duplicates = 0;

        foreach (var food in candidates)
        {
            if (seen.Add(food.FoodId))
            {
                unique.Add(food);
            }
            else
            {
                duplicates++;
            }
        }

        if (duplicates > 0)
        {
            warnings.Add(new PlanWarning
            {
                Code = PlanWarningCodes.DuplicateCandidateFood,
                Message = string.Create(
                    CultureInfo.InvariantCulture,
                    $"Đã bỏ {duplicates} món trùng food_id trong danh mục ứng viên."),
            });
        }

        return unique;
    }

    private static List<CandidateFood> Trim(
        List<CandidateFood> candidates,
        GenerateMealPlanRequest request,
        List<PlanWarning> warnings)
    {
        if (candidates.Count <= PlanDefaults.MaxCandidateFoods)
        {
            return candidates;
        }

        var trimmed = candidates
            .OrderBy(food => MacroFitScore(food, request))
            .ThenBy(food => food.FoodId, StringComparer.Ordinal)
            .Take(PlanDefaults.MaxCandidateFoods)
            .ToList();

        warnings.Add(new PlanWarning
        {
            Code = PlanWarningCodes.CandidateFoodsTruncated,
            Message = string.Create(
                CultureInfo.InvariantCulture,
                $"Đã cắt danh mục ứng viên từ {candidates.Count} xuống {PlanDefaults.MaxCandidateFoods} món theo độ khớp macro."),
        });

        return trimmed;
    }

    private static double MacroEnergy(double protein, double carbs, double fat) =>
        (protein * 4) + (carbs * 4) + (fat * 9);
}

/// <summary>Kết quả lọc danh mục ứng viên kèm warning dữ liệu bị biến đổi.</summary>
public sealed record MealPlanCandidateFilterResult
{
    /// <summary>Danh mục đã dedupe/trim/lọc, sẵn sàng cho prompt và fallback.</summary>
    public required IReadOnlyList<CandidateFood> Candidates { get; init; }

    /// <summary>Warning của bước dedupe/trim.</summary>
    public required IReadOnlyList<PlanWarning> Warnings { get; init; }
}
