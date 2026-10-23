using System.Text;
using Recommendation.Domain.Models;

namespace Recommendation.Application.Features.MealPlans;

/// <summary>
/// Lọc allergen cho hai RPC plan (FR-015/D-03): chuẩn hoá cả hai phía bằng
/// <c>Trim()</c>, <c>ToLowerInvariant()</c> và Unicode <b>NFC</b> rồi so khớp
/// <b>chính xác từng token</b>. Không dùng alias, không so chuỗi con và không suy
/// diễn thành phần từ tên món.
/// </summary>
public sealed class PlanAllergenFilter
{
    /// <summary>
    /// Lọc bỏ mọi món có allergen khớp với hồ sơ. <paramref name="allergies"/> rỗng
    /// nghĩa là không có dị ứng đã biết; <c>CandidateFood.allergens</c> rỗng nghĩa
    /// là caller khai món không có allergen đã biết.
    /// </summary>
    public IReadOnlyList<CandidateFood> Filter(
        IReadOnlyList<CandidateFood> candidates,
        IReadOnlyList<string> allergies)
    {
        ArgumentNullException.ThrowIfNull(candidates);

        if (allergies is null || allergies.Count == 0)
        {
            return candidates.ToList();
        }

        var normalizedAllergies = NormalizeAllergies(allergies);

        return candidates
            .Where(food => !ContainsAny(food, normalizedAllergies))
            .ToList();
    }

    /// <summary><c>true</c> khi món chứa allergen khớp chính xác với hồ sơ.</summary>
    public bool ContainsAllergen(CandidateFood food, IReadOnlyList<string> allergies)
    {
        ArgumentNullException.ThrowIfNull(food);

        if (allergies is null || allergies.Count == 0)
        {
            return false;
        }

        return ContainsAny(food, NormalizeAllergies(allergies));
    }

    /// <summary>Chuẩn hoá một token allergen theo trim + lowercase + NFC.</summary>
    public static string NormalizeToken(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormC);

    private static HashSet<string> NormalizeAllergies(IReadOnlyList<string> allergies)
    {
        var normalized = new HashSet<string>(StringComparer.Ordinal);
        foreach (var allergy in allergies)
        {
            var token = NormalizeToken(allergy);
            if (token.Length > 0)
            {
                normalized.Add(token);
            }
        }

        return normalized;
    }

    private static bool ContainsAny(CandidateFood food, HashSet<string> normalizedAllergies)
    {
        if (normalizedAllergies.Count == 0 || food.Allergens is null || food.Allergens.Count == 0)
        {
            return false;
        }

        foreach (var allergen in food.Allergens)
        {
            var token = NormalizeToken(allergen);
            if (token.Length > 0 && normalizedAllergies.Contains(token))
            {
                return true;
            }
        }

        return false;
    }
}
