using System.Globalization;
using Recommendation.Domain.Constants;
using Recommendation.Domain.Models;

namespace Recommendation.Application.Features.MealPlans;

/// <summary>
/// Kiểm tra kết quả AI của <c>GenerateMealPlan</c> (FR-019): đúng ngày/bữa, tối đa
/// 5 món/bữa, mọi <c>food_id</c> thuộc candidate, không còn allergen và tổng
/// calo/protein/carbs/fat mỗi ngày lệch không quá ±10% ngân sách ngày. Fiber chỉ
/// được báo cáo, không nằm trong gate cứng. Kết quả không hợp lệ buộc use case
/// chạy fallback deterministic.
/// </summary>
public sealed class MealPlanValidator(PlanAllergenFilter allergenFilter)
{
    /// <summary>Kiểm tra toàn bộ các ngày không chạm trần.</summary>
    public MealPlanValidationResult Validate(
        GenerateMealPlanRequest request,
        WeeklyBudget budget,
        IReadOnlyList<MealPlanDay> days,
        IReadOnlyList<CandidateFood> candidates,
        IReadOnlySet<string>? skippedDates = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(budget);
        ArgumentNullException.ThrowIfNull(days);
        ArgumentNullException.ThrowIfNull(candidates);

        var expected = budget.Days
            .Where(day => skippedDates is null || !skippedDates.Contains(day.Date))
            .ToList();

        var lookup = new Dictionary<string, MealPlanDay>(StringComparer.Ordinal);
        foreach (var day in days)
        {
            if (!lookup.TryAdd(day.Date, day))
            {
                return Invalid($"Ngày {day.Date} xuất hiện nhiều lần.");
            }
        }

        if (lookup.Count != expected.Count)
        {
            return Invalid("Số ngày của kế hoạch không khớp yêu cầu.");
        }

        var candidateById = candidates.ToDictionary(food => food.FoodId, StringComparer.Ordinal);

        foreach (var budgetDay in expected)
        {
            if (!lookup.TryGetValue(budgetDay.Date, out var day))
            {
                return Invalid($"Thiếu ngày {budgetDay.Date}.");
            }

            var structural = ValidateStructure(request, budgetDay, day, candidateById);
            if (!structural.IsValid)
            {
                return structural;
            }

            var totals = day.Meals.SelectMany(meal => meal.Foods).ToList();
            var calories = totals.Sum(food => food.Calories);
            var protein = totals.Sum(food => food.ProteinGrams);
            var carbs = totals.Sum(food => food.CarbsGrams);
            var fat = totals.Sum(food => food.FatGrams);

            if (!IsWithinDailyTolerance(calories, protein, carbs, fat, budgetDay))
            {
                return Invalid(
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"Ngày {budgetDay.Date} lệch quá ±10% ngân sách calo/macro."));
            }
        }

        return MealPlanValidationResult.Valid();
    }

    /// <summary>
    /// Gate ±10% dùng chung cho validator và fallback: calo/protein/carbs/fat của
    /// ngày phải nằm trong ±10% ngân sách ngày; mục tiêu bằng 0 được bỏ qua.
    /// </summary>
    public static bool IsWithinDailyTolerance(
        int calories,
        double protein,
        double carbs,
        double fat,
        WeeklyBudget.DayBudget budget) =>
        Within(calories, budget.TargetCalories)
        && Within(protein, budget.TargetProteinGrams)
        && Within(carbs, budget.TargetCarbsGrams)
        && Within(fat, budget.TargetFatGrams);

    private MealPlanValidationResult ValidateStructure(
        GenerateMealPlanRequest request,
        WeeklyBudget.DayBudget budgetDay,
        MealPlanDay day,
        IReadOnlyDictionary<string, CandidateFood> candidateById)
    {
        if (day.CalorieCapReached)
        {
            return Invalid($"Ngày {budgetDay.Date} không được đánh dấu chạm trần ở nhánh AI.");
        }

        if (day.Meals.Count != budgetDay.Meals.Count)
        {
            return Invalid($"Ngày {budgetDay.Date} không đúng số bữa.");
        }

        var expectedSlots = budgetDay.Meals.Select(meal => meal.Slot).ToHashSet();
        var actualSlots = day.Meals.Select(meal => meal.MealType).ToHashSet();
        if (!expectedSlots.SetEquals(actualSlots))
        {
            return Invalid($"Ngày {budgetDay.Date} sai loại bữa.");
        }

        foreach (var meal in day.Meals)
        {
            if (meal.Foods.Count is < 1 or > PlanDefaults.MaxFoodsPerMeal)
            {
                return Invalid($"Bữa {meal.MealType} của ngày {budgetDay.Date} có số món không hợp lệ.");
            }

            foreach (var food in meal.Foods)
            {
                if (!candidateById.TryGetValue(food.FoodId, out var candidate))
                {
                    return Invalid($"Món {food.FoodId} không thuộc danh mục ứng viên.");
                }

                if (allergenFilter.ContainsAllergen(candidate, request.Profile.Allergies))
                {
                    return Invalid($"Món {food.FoodId} chứa allergen của hồ sơ.");
                }
            }
        }

        return MealPlanValidationResult.Valid();
    }

    private static bool Within(double actual, double target)
    {
        if (target <= 0)
        {
            return true;
        }

        return Math.Abs(actual - target) <= target * PlanDefaults.DailyTargetTolerance;
    }

    private static MealPlanValidationResult Invalid(string reason) =>
        new() { IsValid = false, FailureReason = reason };
}

/// <summary>Kết quả kiểm tra kế hoạch do AI trả về.</summary>
public sealed record MealPlanValidationResult
{
    /// <summary>Kế hoạch đạt toàn bộ ràng buộc cấu trúc và ±10%.</summary>
    public required bool IsValid { get; init; }

    /// <summary>Lý do nội bộ khi không hợp lệ; không trả cho caller.</summary>
    public string? FailureReason { get; init; }

    /// <summary>Kết quả hợp lệ dùng lại cho nhiều nhánh.</summary>
    public static MealPlanValidationResult Valid() => new() { IsValid = true };
}
