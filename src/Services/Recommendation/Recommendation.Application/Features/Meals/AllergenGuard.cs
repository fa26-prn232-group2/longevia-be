using Recommendation.Domain.Constants;
using Recommendation.Domain.Models;

namespace Recommendation.Application.Features.Meals;

/// <summary>
/// Loại 100% món chứa thành phần dị ứng của người dùng (FR-002, BR-03). Toàn bộ
/// quy tắc chạy bằng code và **trước** khi gọi mô hình: mô hình không bao giờ
/// nhìn thấy món không an toàn, nên không thể chọn nhầm.
/// </summary>
public sealed class AllergenGuard
{
    /// <summary>Lọc danh mục ứng viên theo danh sách dị ứng đã khai báo.</summary>
    public IReadOnlyList<CandidateFood> Filter(
        IReadOnlyList<CandidateFood> candidates,
        IReadOnlyList<string>? allergies)
    {
        ArgumentNullException.ThrowIfNull(candidates);

        var declared = BuildDeclaredAllergens(allergies);
        if (declared.Count == 0)
        {
            return candidates.ToList();
        }

        return candidates.Where(food => !ContainsDeclaredAllergen(food, declared)).ToList();
    }

    private static List<DeclaredAllergen> BuildDeclaredAllergens(IReadOnlyList<string>? allergies)
    {
        var declared = new List<DeclaredAllergen>();

        if (allergies is null)
        {
            return declared;
        }

        foreach (var allergy in allergies)
        {
            if (string.IsNullOrWhiteSpace(allergy))
            {
                continue;
            }

            declared.Add(new DeclaredAllergen(allergy.Trim(), AllergenAliases.ResolveGroups(allergy)));
        }

        return declared;
    }

    private static bool ContainsDeclaredAllergen(
        CandidateFood food,
        IReadOnlyList<DeclaredAllergen> declared)
    {
        foreach (var allergy in declared)
        {
            if (AllergenAliases.ContainsPhrase(food.Name, allergy.Raw))
            {
                return true;
            }

            foreach (var allergen in food.Allergens)
            {
                if (MatchesAllergenEntry(allergen, allergy))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool MatchesAllergenEntry(string allergen, DeclaredAllergen allergy)
    {
        var containedEitherWay = AllergenAliases.ContainsPhrase(allergen, allergy.Raw)
            || AllergenAliases.ContainsPhrase(allergy.Raw, allergen);

        return containedEitherWay
            || AllergenAliases.ResolveGroups(allergen).Overlaps(allergy.Groups);
    }

    /// <summary>Một dị ứng đã khai báo kèm các nhóm sinh học suy ra từ bảng alias.</summary>
    private sealed record DeclaredAllergen(string Raw, IReadOnlySet<string> Groups);
}
