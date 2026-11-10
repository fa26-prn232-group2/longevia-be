using Recommendation.Domain.Constants;
using Recommendation.Domain.Models;

namespace Recommendation.Application.Features.MealPlans;

/// <summary>
/// Gợi ý nhóm món theo bữa để model không phải tự đoán món nào hợp bữa sáng,
/// bữa chính hay bữa phụ. Chỉ là shortlist trong prompt; validator vẫn kiểm tra
/// toàn bộ candidate nên không nới lỏng ràng buộc.
/// </summary>
internal static class MealCandidateGroups
{
    private static readonly IReadOnlyDictionary<MealSlot, IReadOnlySet<string>> Categories =
        new Dictionary<MealSlot, IReadOnlySet<string>>
        {
            [MealSlot.Breakfast] = Set("Grains", "Dairy", "Eggs", "Fruits", "Beverage"),
            [MealSlot.Lunch] = Set("Meat", "Seafood", "Vegetables", "Grains", "Legumes", "Eggs"),
            [MealSlot.Dinner] = Set("Meat", "Seafood", "Vegetables", "Grains", "Legumes", "Eggs"),
            [MealSlot.Snack] = Set("Fruits", "Dairy", "Nuts", "Beverage"),
        };

    /// <summary>
    /// Trả chỉ số candidate phù hợp bữa, sắp theo độ gần calo bữa. Nếu nhóm quá
    /// ít thì bổ sung từ toàn bộ danh mục để model luôn có đủ lựa chọn.
    /// </summary>
    public static IReadOnlyList<int> IndicesFor(
        MealSlot slot,
        IReadOnlyList<CandidateFood> candidates,
        int mealCalories,
        int take)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        var allowed = Categories.TryGetValue(slot, out var values) ? values : null;

        var preferred = candidates
            .Select((food, index) => (food, index))
            .Where(item => allowed is null
                || string.IsNullOrWhiteSpace(item.food.Category)
                || allowed.Contains(item.food.Category))
            .OrderBy(item => Math.Abs(item.food.Calories - mealCalories))
            .ThenBy(item => item.index)
            .Take(take)
            .Select(item => item.index)
            .ToList();

        if (preferred.Count >= PlanDefaults.MinFoodsPerMeal)
        {
            return preferred;
        }

        var seen = preferred.ToHashSet();
        var filler = candidates
            .Select((food, index) => (food, index))
            .Where(item => !seen.Contains(item.index))
            .OrderBy(item => Math.Abs(item.food.Calories - mealCalories))
            .ThenBy(item => item.index)
            .Take(take - preferred.Count)
            .Select(item => item.index);

        preferred.AddRange(filler);
        return preferred;
    }

    private static IReadOnlySet<string> Set(params string[] values) =>
        new HashSet<string>(values, StringComparer.OrdinalIgnoreCase);
}
