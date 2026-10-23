using System.Text.RegularExpressions;

namespace Recommendation.Application.Features.Search;

/// <summary>
/// Chuẩn hoá câu hỏi phía query trước khi truy xuất: gộp whitespace và cắt độ dài.
/// KHÔNG bỏ dấu tiếng Việt ở đây — việc đó do <c>unaccent</c> trong SQL làm để
/// GIN index vẫn được dùng (D-06).
/// </summary>
public static partial class QueryNormalizer
{
    private const int MaxLength = 2_000;

    /// <summary>Chuẩn hoá câu hỏi; trả chuỗi rỗng nếu đầu vào rỗng.</summary>
    public static string Normalize(string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return string.Empty;
        }

        var collapsed = Whitespace().Replace(query, " ").Trim();
        return collapsed.Length <= MaxLength ? collapsed : collapsed[..MaxLength].TrimEnd();
    }

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex Whitespace();
}
