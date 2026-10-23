using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Recommendation.Domain.Constants;

/// <summary>
/// Bảng ánh xạ thành phần dị ứng tiếng Việt ↔ tiếng Anh/sinh học (T052) và các
/// hàm so khớp thuần văn bản dùng cho <c>AllergenGuard</c>.
/// </summary>
/// <remarks>
/// So khớp luôn bỏ dấu tiếng Việt và không phân biệt hoa thường để "dau phong"
/// khớp "Đậu phộng". Nhóm cha (ví dụ <c>shellfish</c> → <c>seafood</c>) cho phép
/// người dị ứng "hải sản" loại được món ghi cụ thể là "tôm".
/// </remarks>
public static partial class AllergenAliases
{
    /// <summary>Nhóm dị ứng: đậu phộng.</summary>
    public const string Peanut = "peanut";

    /// <summary>Nhóm dị ứng: hạt cây (hạt điều, hạnh nhân, óc chó...).</summary>
    public const string TreeNut = "tree_nut";

    /// <summary>Nhóm dị ứng: sữa và sản phẩm từ sữa.</summary>
    public const string Milk = "milk";

    /// <summary>Nhóm dị ứng: trứng.</summary>
    public const string Egg = "egg";

    /// <summary>Nhóm dị ứng: động vật có vỏ (tôm, cua, sò...).</summary>
    public const string Shellfish = "shellfish";

    /// <summary>Nhóm dị ứng: cá và sản phẩm từ cá.</summary>
    public const string Fish = "fish";

    /// <summary>Nhóm dị ứng: đậu nành và sản phẩm từ đậu nành.</summary>
    public const string Soy = "soy";

    /// <summary>Nhóm dị ứng: gluten/lúa mì.</summary>
    public const string Gluten = "gluten";

    /// <summary>Nhóm dị ứng: vừng (mè).</summary>
    public const string Sesame = "sesame";

    /// <summary>Nhóm dị ứng: hải sản nói chung (cha của <see cref="Shellfish"/> và <see cref="Fish"/>).</summary>
    public const string Seafood = "seafood";

    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> Groups = BuildGroups();

    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> ParentGroups =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
        {
            [Shellfish] = [Seafood],
            [Fish] = [Seafood],
        };

    /// <summary>Chuẩn hoá văn bản để so khớp: bỏ dấu, lowercase, bỏ ký tự không phải chữ/số.</summary>
    public static string Normalize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var decomposed = text.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);

        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            builder.Append(character switch
            {
                'đ' => 'd',
                'Đ' => 'D',
                _ => character,
            });
        }

        return Whitespace().Replace(builder.ToString(), " ").Trim().ToLowerInvariant();
    }

    /// <summary>
    /// Các nhóm dị ứng mà <paramref name="text"/> có thể thuộc về, đã bao gồm
    /// nhóm cha. Trả tập rỗng khi không khớp nhóm nào.
    /// </summary>
    public static IReadOnlySet<string> ResolveGroups(string? text)
    {
        var normalized = Normalize(text);
        var groups = new HashSet<string>(StringComparer.Ordinal);

        if (normalized.Length == 0)
        {
            return groups;
        }

        var tokens = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        foreach (var (group, aliases) in Groups)
        {
            if (aliases.Any(alias => ContainsTokens(tokens, alias.Split(' ', StringSplitOptions.RemoveEmptyEntries))))
            {
                groups.Add(group);
            }
        }

        AddParentGroups(groups);
        return groups;
    }

    /// <summary>
    /// <c>true</c> khi cụm <paramref name="phrase"/> xuất hiện nguyên cụm (theo
    /// ranh giới từ) trong <paramref name="text"/>, sau khi chuẩn hoá hai phía.
    /// </summary>
    public static bool ContainsPhrase(string? text, string? phrase)
    {
        var normalizedText = Normalize(text);
        var normalizedPhrase = Normalize(phrase);

        if (normalizedText.Length == 0 || normalizedPhrase.Length == 0)
        {
            return false;
        }

        var tokens = normalizedText.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var needle = normalizedPhrase.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        return ContainsTokens(tokens, needle);
    }

    private static void AddParentGroups(HashSet<string> groups)
    {
        var pending = new Queue<string>(groups);
        while (pending.Count > 0)
        {
            var current = pending.Dequeue();
            if (!ParentGroups.TryGetValue(current, out var parents))
            {
                continue;
            }

            foreach (var parent in parents)
            {
                if (groups.Add(parent))
                {
                    pending.Enqueue(parent);
                }
            }
        }
    }

    private static bool ContainsTokens(IReadOnlyList<string> tokens, IReadOnlyList<string> needle)
    {
        if (needle.Count == 0 || needle.Count > tokens.Count)
        {
            return false;
        }

        for (var start = 0; start <= tokens.Count - needle.Count; start++)
        {
            if (MatchesAt(tokens, needle, start))
            {
                return true;
            }
        }

        return false;
    }

    private static bool MatchesAt(IReadOnlyList<string> tokens, IReadOnlyList<string> needle, int start)
    {
        for (var offset = 0; offset < needle.Count; offset++)
        {
            if (!string.Equals(tokens[start + offset], needle[offset], StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<string>> BuildGroups()
    {
        var raw = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
        {
            [Peanut] = ["đậu phộng", "đậu phụng", "lạc", "peanut", "groundnut"],
            [TreeNut] =
            [
                "hạt điều", "hạnh nhân", "óc chó", "hạt dẻ", "hồ trăn",
                "cashew", "almond", "walnut", "pistachio", "hazelnut", "tree nut",
            ],
            [Milk] =
            [
                "sữa", "sữa bò", "sữa chua", "phô mai", "bơ sữa", "kem sữa",
                "milk", "dairy", "lactose", "whey", "casein", "cheese", "yogurt", "butter",
            ],
            [Egg] = ["trứng", "trứng gà", "trứng vịt", "egg"],
            [Shellfish] =
            [
                "tôm", "cua", "ghẹ", "sò", "hến", "nghêu", "ốc",
                "shrimp", "prawn", "crab", "shellfish", "lobster",
            ],
            [Fish] =
            [
                "cá", "cá hồi", "cá ngừ", "cá basa", "nước mắm",
                "fish", "salmon", "tuna", "anchovy", "fish sauce",
            ],
            [Soy] = ["đậu nành", "đậu tương", "đậu hũ", "tàu hũ", "soy", "soya", "tofu", "edamame"],
            [Gluten] = ["lúa mì", "bột mì", "bánh mì", "mì", "gluten", "wheat", "barley", "rye"],
            [Sesame] = ["vừng", "mè", "sesame", "tahini"],
            [Seafood] = ["hải sản", "seafood"],
        };

        var normalized = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        foreach (var (group, aliases) in raw)
        {
            normalized[group] = aliases.Select(Normalize).Where(alias => alias.Length > 0).ToList();
        }

        return normalized;
    }

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex Whitespace();
}
