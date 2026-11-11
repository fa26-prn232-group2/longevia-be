using System.Globalization;
using System.Text;

namespace Diet.Domain;

/// <summary>
/// Chuẩn hoá chuỗi tìm kiếm: bỏ dấu tiếng Việt, lowercase, gộp khoảng trắng.
///
/// Dùng cho <c>keyword</c> phía ứng dụng; phía PostgreSQL dùng extension
/// <c>unaccent</c> để chuẩn hoá tên món trước khi so khớp, nhờ đó tìm "ca chua"
/// vẫn ra "Cà chua".
/// </summary>
public static class TextNormalizer
{
    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var decomposed = value.Trim().Normalize(NormalizationForm.FormD);
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

        return string.Join(' ', builder.ToString()
            .Normalize(NormalizationForm.FormC)
            .ToLowerInvariant()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }
}
