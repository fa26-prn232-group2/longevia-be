using System.Text.RegularExpressions;

namespace Recommendation.Domain.Constants;

/// <summary>
/// Ngưỡng và từ khoá của từng sở thích ăn uống (T053). Toàn bộ quy tắc lọc là
/// dữ liệu thuần, để tầng Application chỉ việc áp dụng.
/// </summary>
/// <remarks>
/// Từ khoá <see cref="DietaryPreference.Vegan"/> giữ nguyên dấu tiếng Việt và
/// được so khớp theo ranh giới từ: bỏ dấu sẽ khiến "cá" khớp "cà" (cà chua) và
/// loại oan món chay. Vì vậy tên món phải có dấu đúng như danh mục của caller.
/// </remarks>
public static partial class DietaryPreferenceRules
{
    /// <summary>Ngưỡng carbohydrate tối đa mỗi khẩu phần của Keto (g).</summary>
    public const double KetoMaxCarbsGrams = 10;

    /// <summary>Ngưỡng carbohydrate tối đa mỗi khẩu phần của Low-Carb (g).</summary>
    public const double LowCarbMaxCarbsGrams = 30;

    private static readonly IReadOnlyList<string> VeganExcludedKeywords =
    [
        "thịt", "bò", "bê", "gà", "vịt", "heo", "lợn", "trứng",
        "cá", "tôm", "cua", "mực", "sò", "hến", "nghêu",
        "sữa", "phô mai", "bơ sữa", "kem", "yogurt", "mật ong", "nước mắm",
    ];

    /// <summary>Đọc sở thích từ hồ sơ; giá trị lạ/thiếu coi như <see cref="DietaryPreference.None"/>.</summary>
    public static DietaryPreference Parse(string? value) => AllergenAliases.Normalize(value) switch
    {
        "keto" => DietaryPreference.Keto,
        "vegan" or "thuan chay" or "chay" => DietaryPreference.Vegan,
        "mediterranean" or "dia trung hai" => DietaryPreference.Mediterranean,
        "lowcarb" or "low carb" or "it tinh bot" => DietaryPreference.LowCarb,
        _ => DietaryPreference.None,
    };

    /// <summary>Ngưỡng carbohydrate mỗi khẩu phần của sở thích; <c>null</c> nghĩa là không giới hạn.</summary>
    public static double? MaxCarbsGrams(DietaryPreference preference) => preference switch
    {
        DietaryPreference.Keto => KetoMaxCarbsGrams,
        DietaryPreference.LowCarb => LowCarbMaxCarbsGrams,
        _ => null,
    };

    /// <summary>Từ khoá thực phẩm động vật bị loại với <see cref="DietaryPreference.Vegan"/>.</summary>
    public static IReadOnlyList<string> ExcludedKeywords(DietaryPreference preference) =>
        preference == DietaryPreference.Vegan ? VeganExcludedKeywords : [];

    /// <summary><c>true</c> khi <paramref name="name"/> chứa <paramref name="keyword"/> nguyên cụm từ.</summary>
    public static bool ContainsKeyword(string? name, string keyword)
    {
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(keyword))
        {
            return false;
        }

        var tokens = Tokenize(name);
        var needle = Tokenize(keyword);

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

    private static List<string> Tokenize(string text) =>
        WordSeparator()
            .Split(text.ToLowerInvariant())
            .Where(token => token.Length > 0)
            .ToList();

    [GeneratedRegex(@"[^\p{L}\p{Nd}]+", RegexOptions.CultureInvariant)]
    private static partial Regex WordSeparator();
}
