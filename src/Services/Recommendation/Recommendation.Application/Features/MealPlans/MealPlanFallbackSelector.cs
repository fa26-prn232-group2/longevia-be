using Recommendation.Application.Features.Plans;
using Recommendation.Domain.Constants;
using Recommendation.Domain.Models;

namespace Recommendation.Application.Features.MealPlans;

/// <summary>
/// Phương án dự phòng xác định cho <c>GenerateMealPlan</c> (D-07/FR-020/FR-021):
/// duyệt candidate an toàn theo macro-fit, thêm món nếu làm tổng bữa gần ngân sách
/// bữa hơn, không lặp món trong cùng ngày và hạn chế lặp trong tuần bằng tie-break
/// (ít lần dùng nhất, rồi <c>food_id</c> ordinal). Cùng đầu vào luôn cho cùng kết quả.
/// </summary>
public sealed class MealPlanFallbackSelector(PlanRationaleBuilder rationaleBuilder)
{
    /// <summary>Chọn kế hoạch dự phòng cho các ngày không chạm trần calo.</summary>
    public MealPlanFallbackResult Select(
        GenerateMealPlanRequest request,
        WeeklyBudget budget,
        IReadOnlyList<CandidateFood> candidates,
        IReadOnlySet<string>? skippedDates = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(budget);
        ArgumentNullException.ThrowIfNull(candidates);

        var ordered = candidates
            .OrderBy(food => MealPlanCandidateFilter.MacroFitScore(food, request))
            .ThenBy(food => food.FoodId, StringComparer.Ordinal)
            .ToList();

        var weeklyUsage = new Dictionary<string, int>(StringComparer.Ordinal);
        var days = new List<MealPlanDay>(budget.Days.Count);
        var targetNotMet = false;

        foreach (var budgetDay in budget.Days)
        {
            if (skippedDates is not null && skippedDates.Contains(budgetDay.Date))
            {
                continue;
            }

            var meals = BuildMeals(budgetDay, ordered, weeklyUsage);
            var allFoods = meals.SelectMany(meal => meal.Foods).ToList();
            var calories = allFoods.Sum(food => food.Calories);
            var protein = allFoods.Sum(food => food.ProteinGrams);
            var carbs = allFoods.Sum(food => food.CarbsGrams);
            var fat = allFoods.Sum(food => food.FatGrams);

            if (!MealPlanValidator.IsWithinDailyTolerance(calories, protein, carbs, fat, budgetDay))
            {
                targetNotMet = true;
            }

            days.Add(new MealPlanDay
            {
                Date = budgetDay.Date,
                TargetCalories = budgetDay.TargetCalories,
                TargetProteinGrams = budgetDay.TargetProteinGrams,
                TargetCarbsGrams = budgetDay.TargetCarbsGrams,
                TargetFatGrams = budgetDay.TargetFatGrams,
                TargetFiberGrams = budgetDay.TargetFiberGrams,
                TotalCalories = calories,
                TotalProteinGrams = protein,
                TotalCarbsGrams = carbs,
                TotalFatGrams = fat,
                TotalFiberGrams = allFoods.Sum(food => food.FiberGrams),
                Meals = meals,
            });
        }

        return new MealPlanFallbackResult
        {
            Days = days,
            TargetNotMet = targetNotMet,
        };
    }

    private List<PlannedMeal> BuildMeals(
        WeeklyBudget.DayBudget budgetDay,
        IReadOnlyList<CandidateFood> ordered,
        Dictionary<string, int> weeklyUsage)
    {
        var meals = new List<PlannedMeal>(budgetDay.Meals.Count);
        var usedToday = new HashSet<string>(StringComparer.Ordinal);

        for (var mealIndex = 0; mealIndex < budgetDay.Meals.Count; mealIndex++)
        {
            var mealBudget = budgetDay.Meals[mealIndex];
            var remainingMeals = budgetDay.Meals.Count - mealIndex;
            var foods = BuildMeal(budgetDay, mealBudget, ordered, usedToday, weeklyUsage, remainingMeals);

            foreach (var food in foods)
            {
                weeklyUsage[food.FoodId] = weeklyUsage.GetValueOrDefault(food.FoodId) + 1;
                usedToday.Add(food.FoodId);
            }

            meals.Add(new PlannedMeal
            {
                MealType = mealBudget.Slot,
                TargetCalories = mealBudget.TargetCalories,
                Foods = foods.Select(ToPlannedFood).ToList(),
                Rationale = rationaleBuilder.BuildMealRationale(mealBudget.Slot, foods, []),
                Citations = [],
            });
        }

        return meals;
    }

    private static List<CandidateFood> BuildMeal(
        WeeklyBudget.DayBudget budgetDay,
        WeeklyBudget.MealBudget mealBudget,
        IReadOnlyList<CandidateFood> ordered,
        HashSet<string> usedToday,
        Dictionary<string, int> weeklyUsage,
        int remainingMeals)
    {
        var foods = new List<CandidateFood>(PlanDefaults.MaxFoodsPerMeal);

        // Ưu tiên món chưa dùng trong ngày. Chỉ mở pool tái sử dụng khi số món
        // chưa dùng không đủ để mỗi bữa còn lại có ít nhất một món; nhờ đó kế
        // hoạch fallback không bao giờ có bữa rỗng (SC-002/FR-020).
        var unusedPool = ordered.Where(food => !usedToday.Contains(food.FoodId)).ToList();
        var reuseAllowed = unusedPool.Count < remainingMeals;
        var reusedPool = reuseAllowed
            ? ordered.Where(food => usedToday.Contains(food.FoodId)).ToList()
            : [];

        var maxFoods = reuseAllowed
            ? PlanDefaults.MaxFoodsPerMeal
            : Math.Max(
                1,
                Math.Min(
                    PlanDefaults.MaxFoodsPerMeal,
                    unusedPool.Count - (remainingMeals - 1)));

        var usedInMeal = new HashSet<string>(StringComparer.Ordinal);
        var calories = 0;
        var protein = 0d;
        var carbs = 0d;
        var fat = 0d;
        var currentError = MealError(calories, protein, carbs, fat, mealBudget);

        while (foods.Count < maxFoods)
        {
            var primary = FindBest(
                unusedPool,
                usedInMeal,
                calories,
                protein,
                carbs,
                fat,
                mealBudget,
                weeklyUsage);

            (CandidateFood Food, double Error)? chosen;
            if (foods.Count == 0)
            {
                // Bữa đầu tiên luôn phải có món: dùng món chưa dùng nếu còn,
                // nếu không mới tái sử dụng món của bữa khác trong ngày.
                chosen = primary ?? (reuseAllowed
                    ? FindBest(reusedPool, usedInMeal, calories, protein, carbs, fat, mealBudget, weeklyUsage)
                    : null);
            }
            else if (primary is { } primaryCandidate
                && primaryCandidate.Error < currentError - 1e-9)
            {
                chosen = primaryCandidate;
            }
            else
            {
                var reused = reuseAllowed
                    ? FindBest(reusedPool, usedInMeal, calories, protein, carbs, fat, mealBudget, weeklyUsage)
                    : null;

                chosen = reused is { } reusedCandidate && reusedCandidate.Error < currentError - 1e-9
                    ? reusedCandidate
                    : null;
            }

            if (chosen is not { } selection)
            {
                break;
            }

            foods.Add(selection.Food);
            usedInMeal.Add(selection.Food.FoodId);
            calories += selection.Food.Calories;
            protein += selection.Food.ProteinGrams;
            carbs += selection.Food.CarbsGrams;
            fat += selection.Food.FatGrams;
            currentError = selection.Error;
        }

        return foods;
    }

    private static (CandidateFood Food, double Error)? FindBest(
        IReadOnlyList<CandidateFood> pool,
        HashSet<string> usedInMeal,
        int calories,
        double protein,
        double carbs,
        double fat,
        WeeklyBudget.MealBudget mealBudget,
        Dictionary<string, int> weeklyUsage)
    {
        (CandidateFood Food, double Error)? best = null;

        foreach (var food in pool)
        {
            if (usedInMeal.Contains(food.FoodId))
            {
                continue;
            }

            var error = MealError(
                calories + food.Calories,
                protein + food.ProteinGrams,
                carbs + food.CarbsGrams,
                fat + food.FatGrams,
                mealBudget);

            if (best is not { } current
                || error < current.Error - 1e-9
                || (Math.Abs(error - current.Error) <= 1e-9
                    && IsPreferred(food, current.Food, weeklyUsage)))
            {
                best = (food, error);
            }
        }

        return best;
    }

    private static bool IsPreferred(
        CandidateFood candidate,
        CandidateFood current,
        Dictionary<string, int> weeklyUsage)
    {
        var candidateUsage = weeklyUsage.GetValueOrDefault(candidate.FoodId);
        var currentUsage = weeklyUsage.GetValueOrDefault(current.FoodId);

        return candidateUsage != currentUsage
            ? candidateUsage < currentUsage
            : string.CompareOrdinal(candidate.FoodId, current.FoodId) < 0;
    }

    private static double MealError(
        int calories,
        double protein,
        double carbs,
        double fat,
        WeeklyBudget.MealBudget budget) =>
        Relative(calories, budget.TargetCalories)
        + Relative(protein, budget.TargetProteinGrams)
        + Relative(carbs, budget.TargetCarbsGrams)
        + Relative(fat, budget.TargetFatGrams);

    private static double Relative(double actual, double target) =>
        target <= 0 ? 0 : Math.Abs(actual - target) / target;

    private static PlannedFood ToPlannedFood(CandidateFood food) => new()
    {
        FoodId = food.FoodId,
        Name = food.Name,
        ServingSize = food.ServingSize,
        ServingUnit = food.ServingUnit,
        Calories = food.Calories,
        ProteinGrams = food.ProteinGrams,
        CarbsGrams = food.CarbsGrams,
        FatGrams = food.FatGrams,
        FiberGrams = food.FiberGrams,
        Allergens = food.Allergens,
    };
}

/// <summary>Kết quả fallback: kế hoạch và cờ nội bộ khi candidate không đủ đạt ±10%.</summary>
public sealed record MealPlanFallbackResult
{
    /// <summary>Các ngày không chạm trần, theo đúng thứ tự ngân sách.</summary>
    public required IReadOnlyList<MealPlanDay> Days { get; init; }

    /// <summary><c>true</c> khi có ngày không đạt ±10% vì candidate không đủ.</summary>
    public bool TargetNotMet { get; init; }
}
