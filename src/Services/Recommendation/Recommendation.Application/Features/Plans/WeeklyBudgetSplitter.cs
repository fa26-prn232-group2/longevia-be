using Recommendation.Application.Features.MealPlans;
using Recommendation.Domain.Constants;
using Recommendation.Domain.Models;

namespace Recommendation.Application.Features.Plans;

/// <summary>
/// Chia mục tiêu <b>ngày</b> của caller cho từng bữa theo tỷ lệ cố định D-04.
/// Tổng ngân sách các ngày bằng mục tiêu ngày × <c>days</c>; tổng ngân sách các
/// bữa trong ngày bằng ngân sách ngày; phần lẻ do làm tròn calo được bù vào bữa
/// cuối (FR-016).
/// </summary>
public sealed class WeeklyBudgetSplitter(PlanDateRange dateRange)
{
    /// <summary>Chia ngân sách cho toàn bộ ngày của request.</summary>
    public WeeklyBudget Split(GenerateMealPlanRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var dates = dateRange.Build(request.WeekStartDate, request.Days);
        var shares = PlanDefaults.MealShares(request.MealsPerDay);
        var days = new List<WeeklyBudget.DayBudget>(dates.Count);

        foreach (var date in dates)
        {
            days.Add(BuildDay(date, request, shares));
        }

        return new WeeklyBudget { Days = days };
    }

    private static WeeklyBudget.DayBudget BuildDay(
        string date,
        GenerateMealPlanRequest request,
        IReadOnlyList<(MealSlot Slot, double Ratio)> shares)
    {
        var meals = new List<WeeklyBudget.MealBudget>(shares.Count);
        var allocatedCalories = 0;
        var allocatedProtein = 0d;
        var allocatedCarbs = 0d;
        var allocatedFat = 0d;
        var allocatedFiber = 0d;

        for (var index = 0; index < shares.Count; index++)
        {
            var (slot, ratio) = shares[index];
            var isLast = index == shares.Count - 1;

            var calories = isLast
                ? request.TargetCalories - allocatedCalories
                : (int)Math.Round(
                    request.TargetCalories * ratio,
                    MidpointRounding.AwayFromZero);

            var protein = isLast
                ? request.TargetProteinGrams - allocatedProtein
                : request.TargetProteinGrams * ratio;
            var carbs = isLast
                ? request.TargetCarbsGrams - allocatedCarbs
                : request.TargetCarbsGrams * ratio;
            var fat = isLast
                ? request.TargetFatGrams - allocatedFat
                : request.TargetFatGrams * ratio;
            var fiber = isLast
                ? request.TargetFiberGrams - allocatedFiber
                : request.TargetFiberGrams * ratio;

            allocatedCalories += calories;
            allocatedProtein += protein;
            allocatedCarbs += carbs;
            allocatedFat += fat;
            allocatedFiber += fiber;

            meals.Add(new WeeklyBudget.MealBudget
            {
                Slot = slot,
                TargetCalories = calories,
                TargetProteinGrams = Round(protein),
                TargetCarbsGrams = Round(carbs),
                TargetFatGrams = Round(fat),
                TargetFiberGrams = Round(fiber),
            });
        }

        return new WeeklyBudget.DayBudget
        {
            Date = date,
            TargetCalories = request.TargetCalories,
            TargetProteinGrams = request.TargetProteinGrams,
            TargetCarbsGrams = request.TargetCarbsGrams,
            TargetFatGrams = request.TargetFatGrams,
            TargetFiberGrams = request.TargetFiberGrams,
            Meals = meals,
        };
    }

    private static double Round(double value) =>
        Math.Round(value, 6, MidpointRounding.AwayFromZero);
}
