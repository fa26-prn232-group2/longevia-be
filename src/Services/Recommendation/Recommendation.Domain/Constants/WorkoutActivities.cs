using System.Globalization;
using System.Text;

namespace Recommendation.Domain.Constants;

/// <summary>
/// Một hoạt động trong danh mục đóng của chương 5 (D-09/FR-027/FR-029): key ổn
/// định, alias tiếng Việt/Anh để đối chiếu với sách và preference caller, thiết
/// bị tối thiểu, cùng cường độ chỉ được dùng khi chunk nguồn có căn cứ.
/// </summary>
public sealed record WorkoutActivityDefinition
{
    /// <summary>Key ổn định trả cho caller; không đổi sau khi phát hành.</summary>
    public required string Key { get; init; }

    /// <summary>Tên tiếng Việt theo sách, dùng cho prompt và rationale.</summary>
    public required string DisplayName { get; init; }

    /// <summary>Alias nhận diện hoạt động trong chương 5 và preference caller.</summary>
    public required IReadOnlyList<string> Aliases { get; init; }

    /// <summary>Token thiết bị caller có thể gửi để đáp ứng yêu cầu của hoạt động.</summary>
    public IReadOnlyList<string> EquipmentAliases { get; init; } = [];

    /// <summary>Thiết bị đã dùng trả trong contract; rỗng khi không cần thiết bị.</summary>
    public IReadOnlyList<string> EquipmentUsed { get; init; } = [];

    /// <summary>Cường độ khai báo từ chương 5; rỗng khi sách không nêu cường độ.</summary>
    public string Intensity { get; init; } = string.Empty;

    /// <summary>Cụm từ phải xuất hiện trong chunk chương 5 thì cường độ mới được dùng.</summary>
    public string IntensityEvidence { get; init; } = string.Empty;
}

/// <summary>
/// Danh mục hoạt động đóng lấy từ chương 5 của sách tham chiếu. Chỉ các hoạt động
/// ở đây mới được xuất hiện trong kế hoạch tập luyện (FR-008/FR-009).
/// </summary>
public static class WorkoutActivities
{
    /// <summary>Chương duy nhất được phép làm căn cứ cho workout plan (FR-027).</summary>
    public const int SourceChapterNumber = 5;

    /// <summary>Toàn bộ hoạt động, theo thứ tự ổn định dùng cho phân bổ xác định.</summary>
    public static IReadOnlyList<WorkoutActivityDefinition> Definitions { get; } =
    [
        new WorkoutActivityDefinition
        {
            Key = "walking",
            DisplayName = "Đi bộ",
            Aliases = ["walking", "walk", "đi bộ", "đi bộ nhanh", "đi bộ đường dài"],
            Intensity = "Trung bình (3-6 MET)",
            IntensityEvidence = "Đi bộ nhanh",
        },
        new WorkoutActivityDefinition
        {
            Key = "cycling",
            DisplayName = "Đạp xe",
            Aliases = ["cycling", "bicycling", "bike", "đạp xe", "xe đạp"],
            EquipmentAliases = ["bike", "bicycle", "xe đạp", "xe dap"],
            EquipmentUsed = ["bike"],
            Intensity = "Trung bình (3-6 MET)",
            IntensityEvidence = "Đạp xe 10-12 dặm/giờ",
        },
        new WorkoutActivityDefinition
        {
            Key = "running",
            DisplayName = "Chạy bộ",
            Aliases = ["running", "run", "jogging", "chạy", "chạy bộ"],
            Intensity = "Trung bình (3-6 MET)",
            IntensityEvidence = "Chạy thể dục tốc độ chậm",
        },
        new WorkoutActivityDefinition
        {
            Key = "swimming",
            DisplayName = "Bơi",
            Aliases = ["swimming", "swim", "bơi", "bơi lội"],
            EquipmentAliases = ["pool", "swimming pool", "bể bơi", "be boi", "hồ bơi"],
            EquipmentUsed = ["pool"],
        },
        new WorkoutActivityDefinition
        {
            Key = "strength",
            DisplayName = "Tập tạ",
            Aliases = ["strength", "strength training", "weight training", "tập tạ", "nâng tạ", "tạ", "chống đẩy"],
            EquipmentAliases = ["gym", "phòng gym", "phong gym", "weights", "dumbbell", "dumbbells", "tạ"],
            EquipmentUsed = ["gym"],
        },
        new WorkoutActivityDefinition
        {
            Key = "tai_chi",
            DisplayName = "Thái cực quyền",
            Aliases = ["tai chi", "tai_chi", "thái cực quyền", "thái cực"],
        },
        new WorkoutActivityDefinition
        {
            Key = "dancing",
            DisplayName = "Nhảy múa",
            Aliases = ["dancing", "dance", "nhảy múa", "khiêu vũ", "nhảy"],
        },
        new WorkoutActivityDefinition
        {
            Key = "gardening",
            DisplayName = "Làm vườn",
            Aliases = ["gardening", "garden", "làm vườn", "trồng trọt"],
            Intensity = "Trung bình (3-6 MET)",
            IntensityEvidence = "Làm vườn",
        },
    ];

    /// <summary>Tìm hoạt động theo key ổn định; <c>null</c> khi không thuộc danh mục.</summary>
    public static WorkoutActivityDefinition? FindByKey(string? key)
    {
        var normalized = Normalize(key);
        if (normalized.Length == 0)
        {
            return null;
        }

        foreach (var definition in Definitions)
        {
            if (string.Equals(definition.Key, normalized, StringComparison.Ordinal))
            {
                return definition;
            }
        }

        return null;
    }

    /// <summary><c>true</c> khi giá trị caller gửi khớp key hoặc alias của hoạt động.</summary>
    public static bool MatchesAlias(WorkoutActivityDefinition definition, string? value)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var normalized = NormalizeForMatch(value);
        if (normalized.Length == 0)
        {
            return false;
        }

        if (string.Equals(NormalizeForMatch(definition.Key), normalized, StringComparison.Ordinal))
        {
            return true;
        }

        return definition.Aliases.Any(
            alias => string.Equals(NormalizeForMatch(alias), normalized, StringComparison.Ordinal));
    }

    /// <summary>
    /// <c>true</c> khi caller có đủ thiết bị cho hoạt động hoặc hoạt động không cần
    /// thiết bị. So khớp alias thiết bị sau khi chuẩn hoá dấu/hoa-thường.
    /// </summary>
    public static bool SatisfiesEquipment(
        WorkoutActivityDefinition definition,
        IReadOnlySet<string> normalizedEquipment)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(normalizedEquipment);

        if (definition.EquipmentAliases.Count == 0)
        {
            return true;
        }

        foreach (var alias in definition.EquipmentAliases)
        {
            var normalizedAlias = NormalizeForMatch(alias);
            if (normalizedEquipment.Contains(normalizedAlias))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Chuẩn hoá token (preference/equipment/key): trim, lowercase, Unicode NFC và
    /// gộp khoảng trắng. Dùng cho so khớp chính xác.
    /// </summary>
    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var parts = value
            .Normalize(NormalizationForm.FormC)
            .Trim()
            .ToLowerInvariant()
            .Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries);

        return string.Join(' ', parts);
    }

    /// <summary>
    /// Chuẩn hoá để đối chiếu với nội dung sách: bỏ dấu, <c>đ</c> → <c>d</c>, gộp
    /// khoảng trắng và lowercase (cùng quy ước với <c>CitationResolver</c>).
    /// </summary>
    public static string NormalizeForMatch(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var decomposed = value.Normalize(NormalizationForm.FormD);
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

        return string.Join(
            ' ',
            builder
                .ToString()
                .ToLowerInvariant()
                .Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries));
    }

    /// <summary>
    /// <c>true</c> khi <paramref name="text"/> chứa <paramref name="phrase"/> như
    /// một dãy token trọn vẹn (không khớp chuỗi con giữa hai từ).
    /// </summary>
    public static bool ContainsPhrase(string? text, string? phrase)
    {
        var haystack = NormalizeForMatch(text);
        var needle = NormalizeForMatch(phrase);

        if (haystack.Length == 0 || needle.Length == 0)
        {
            return false;
        }

        var start = 0;
        while (start <= haystack.Length - needle.Length)
        {
            var index = haystack.IndexOf(needle, start, StringComparison.Ordinal);
            if (index < 0)
            {
                return false;
            }

            var beforeOk = index == 0 || !char.IsLetterOrDigit(haystack[index - 1]);
            var endIndex = index + needle.Length;
            var afterOk = endIndex >= haystack.Length || !char.IsLetterOrDigit(haystack[endIndex]);

            if (beforeOk && afterOk)
            {
                return true;
            }

            start = index + 1;
        }

        return false;
    }
}
