using System.Globalization;
using System.Text;

namespace Diet.Domain;

/// <summary>
/// Chuẩn hoá chuỗi tìm kiếm: bỏ dấu tiếng Việt, lowercase, gộp khoảng trắng.
///
/// Dùng cho <c>keyword</c> phía ứng dụng; phía PostgreSQL dùng extension
/// <c>unaccent</c> để chuẩn hoá tên món trước khi so khớp, nhờ đó tìm "ca chua"
/// vẫn ra "Cà chua".
///
/// Cố ý KHÔNG dùng <see cref="string.Normalize()"/>: image runtime của Diet
/// (<c>aspnet:*-chiseled</c>) không có ICU và chạy ở chế độ invariant globalization, khi đó
/// <c>Normalize(FormD)</c> không tách dấu nên "Chuối" vẫn là "chuối" và không khớp tên món
/// đã bỏ dấu phía SQL. Bảng bỏ dấu bên dưới cho cùng kết quả dù có ICU hay không.
/// </summary>
public static class TextNormalizer
{
    private static readonly Dictionary<char, char> Folding = BuildFolding();

    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(value.Length);
        var pendingSpace = false;
        foreach (var raw in value.Trim())
        {
            if (char.IsWhiteSpace(raw))
            {
                pendingSpace = builder.Length > 0;
                continue;
            }

            // Dấu rời (khi người dùng gõ/dán dạng tổ hợp NFD) cũng bị bỏ.
            if (CharUnicodeInfo.GetUnicodeCategory(raw) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            var lowered = char.ToLowerInvariant(raw);
            if (pendingSpace)
            {
                builder.Append(' ');
                pendingSpace = false;
            }

            builder.Append(Folding.GetValueOrDefault(lowered, lowered));
        }

        return builder.ToString();
    }

    private static Dictionary<char, char> BuildFolding()
    {
        var map = new Dictionary<char, char>();
        Add(map, 'a', "àáạảãâầấậẩẫăằắặẳẵ");
        Add(map, 'e', "èéẹẻẽêềếệểễ");
        Add(map, 'i', "ìíịỉĩ");
        Add(map, 'o', "òóọỏõôồốộổỗơờớợởỡ");
        Add(map, 'u', "ùúụủũưừứựửữ");
        Add(map, 'y', "ỳýỵỷỹ");
        Add(map, 'd', "đ");
        return map;
    }

    private static void Add(Dictionary<char, char> map, char plain, string accented)
    {
        foreach (var c in accented)
        {
            map[c] = plain;
        }
    }
}
