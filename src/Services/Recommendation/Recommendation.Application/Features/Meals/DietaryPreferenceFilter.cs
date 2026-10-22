using Recommendation.Domain.Constants;
using Recommendation.Domain.Models;

namespace Recommendation.Application.Features.Meals;

/// <summary>
/// Lọc món theo sở thích ăn uống đã khai báo (FR-003). Quy tắc cũng chạy bằng
/// code trước khi gọi mô hình; <c>None</c> và <c>Mediterranean</c> không loại trừ.
/// </summary>
public sealed class DietaryPreferenceFilter
{
    /// <summary>Lọc danh mục theo sở thích; giá trị lạ được coi là <c>None</c>.</summary>
    public IReadOnlyList<CandidateFood> Filter(
        IReadOnlyList<CandidateFood> candidates,
        string? dietaryPreference)
    {
        ArgumentNullException.ThrowIfNull(candidates);

        var preference = DietaryPreferenceRules.Parse(dietaryPreference);
        if (preference is DietaryPreference.None or DietaryPreference.Mediterranean)
        {
            return candidates.ToList();
        }

        return candidates.Where(food => IsAllowed(food, preference)).ToList();
    }

    private static bool IsAllowed(CandidateFood food, DietaryPreference preference)
    {
        var maxCarbs = DietaryPreferenceRules.MaxCarbsGrams(preference);
        if (maxCarbs is not null && food.CarbsGrams > maxCarbs.Value)
        {
            return false;
        }

        foreach (var keyword in DietaryPreferenceRules.ExcludedKeywords(preference))
        {
            if (DietaryPreferenceRules.ContainsKeyword(food.Name, keyword))
            {
                return false;
            }
        }

        return true;
    }
}
