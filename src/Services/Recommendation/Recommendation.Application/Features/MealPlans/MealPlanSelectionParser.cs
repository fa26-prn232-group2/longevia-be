using System.Text.Json.Nodes;
using Recommendation.Application.Features.Ask;
using Recommendation.Application.Features.Plans;
using Recommendation.Domain.Constants;
using Recommendation.Domain.Models;

namespace Recommendation.Application.Features.MealPlans;

/// <summary>
/// Đọc payload <c>MealPlanSelection</c> của mô hình (D-06): chỉ đọc
/// <c>days[].meals[].food_indices[]</c> và citation; dinh dưỡng được sao chép
/// nguyên từ candidate, rationale do code dựng. Ngày/bữa thiếu hoặc chỉ số sai
/// được giữ ở dạng không hợp lệ để <see cref="MealPlanValidator"/> từ chối và use
/// case chuyển sang fallback.
/// </summary>
public sealed class MealPlanSelectionParser(
    CitationResolver citationResolver,
    PlanRationaleBuilder rationaleBuilder)
{
    /// <summary>Đọc kết quả chọn món; bỏ qua các ngày đã chạm trần calo.</summary>
    public IReadOnlyList<MealPlanDay> Parse(
        JsonObject? payload,
        IReadOnlyList<CandidateFood> candidates,
        IReadOnlyList<RetrievedChunk> chunks,
        WeeklyBudget budget,
        IReadOnlySet<string>? skippedDates = null)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(chunks);
        ArgumentNullException.ThrowIfNull(budget);

        var days = new List<MealPlanDay>(budget.Days.Count);

        foreach (var budgetDay in budget.Days)
        {
            if (skippedDates is not null && skippedDates.Contains(budgetDay.Date))
            {
                continue;
            }

            days.Add(ParseDay(payload, budgetDay, candidates, chunks));
        }

        return days;
    }

    private MealPlanDay ParseDay(
        JsonObject? payload,
        WeeklyBudget.DayBudget budgetDay,
        IReadOnlyList<CandidateFood> candidates,
        IReadOnlyList<RetrievedChunk> chunks)
    {
        var dayNode = FindDay(payload, budgetDay.Date);
        var meals = new List<PlannedMeal>(budgetDay.Meals.Count);

        foreach (var mealBudget in budgetDay.Meals)
        {
            var mealNode = FindMeal(dayNode, mealBudget.Slot);
            var foods = ReadFoods(mealNode, candidates);
            var citations = citationResolver.ResolveOptional(mealNode, chunks);

            meals.Add(new PlannedMeal
            {
                MealType = mealBudget.Slot,
                TargetCalories = mealBudget.TargetCalories,
                Foods = foods.Select(ToPlannedFood).ToList(),
                Rationale = rationaleBuilder.BuildMealRationale(mealBudget.Slot, foods, citations),
                Citations = citations,
            });
        }

        var allFoods = meals.SelectMany(meal => meal.Foods).ToList();

        return new MealPlanDay
        {
            Date = budgetDay.Date,
            TargetCalories = budgetDay.TargetCalories,
            TargetProteinGrams = budgetDay.TargetProteinGrams,
            TargetCarbsGrams = budgetDay.TargetCarbsGrams,
            TargetFatGrams = budgetDay.TargetFatGrams,
            TargetFiberGrams = budgetDay.TargetFiberGrams,
            TotalCalories = allFoods.Sum(food => food.Calories),
            TotalProteinGrams = allFoods.Sum(food => food.ProteinGrams),
            TotalCarbsGrams = allFoods.Sum(food => food.CarbsGrams),
            TotalFatGrams = allFoods.Sum(food => food.FatGrams),
            TotalFiberGrams = allFoods.Sum(food => food.FiberGrams),
            Meals = meals,
        };
    }

    private static JsonObject? FindDay(JsonObject? payload, string date)
    {
        if (payload?["days"] is not JsonArray days)
        {
            return null;
        }

        foreach (var node in days)
        {
            if (node is JsonObject day
                && day["date"] is JsonValue value
                && value.TryGetValue<string>(out var dayDate)
                && string.Equals(dayDate, date, StringComparison.Ordinal))
            {
                return day;
            }
        }

        return null;
    }

    private static JsonObject? FindMeal(JsonObject? dayNode, MealSlot slot)
    {
        if (dayNode?["meals"] is not JsonArray meals)
        {
            return null;
        }

        var expected = slot.ToString();

        foreach (var node in meals)
        {
            if (node is JsonObject meal
                && meal["meal_type"] is JsonValue value
                && value.TryGetValue<string>(out var mealType)
                && string.Equals(mealType, expected, StringComparison.OrdinalIgnoreCase))
            {
                return meal;
            }
        }

        return null;
    }

    private static List<CandidateFood> ReadFoods(
        JsonObject? mealNode,
        IReadOnlyList<CandidateFood> candidates)
    {
        var foods = new List<CandidateFood>(PlanDefaults.MaxFoodsPerMeal);

        if (mealNode?["food_indices"] is not JsonArray indices)
        {
            return foods;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in indices)
        {
            if (node is not JsonValue value || !value.TryGetValue<int>(out var index))
            {
                continue;
            }

            if (index < 0 || index >= candidates.Count)
            {
                continue;
            }

            var food = candidates[index];
            if (seen.Add(food.FoodId))
            {
                foods.Add(food);
            }
        }

        return foods;
    }

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
