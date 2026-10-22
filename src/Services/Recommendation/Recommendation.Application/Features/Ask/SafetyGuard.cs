using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Recommendation.Domain.Constants;

namespace Recommendation.Application.Features.Ask;

/// <summary>
/// Chặn câu hỏi ngoài phạm vi sách trước khi gọi provider (FR-033): cụm từ chỉ
/// định thuốc/liều thuốc, chẩn đoán bệnh hoặc thay thế điều trị. Quyết định
/// deterministic bằng cụm từ, không phụ thuộc prompt. Chỉ so khớp nguyên cụm
/// (sau khi bỏ dấu) nên câu hỏi lối sống có chữ "thuốc" như "hút thuốc lá có hại
/// gì?" vẫn được trả lời bình thường, và câu hỏi về chế độ ăn cho người mắc bệnh
/// vẫn trả ở mức lối sống (FR-032).
/// </summary>
public sealed partial class SafetyGuard
{
    /// <summary>Thông điệp từ chối, hướng người dùng tới bác sĩ chuyên khoa.</summary>
    public const string RefusalMessage =
        "Câu hỏi về thuốc, liều thuốc, chẩn đoán bệnh hoặc thay thế điều trị nằm ngoài phạm vi "
        + "nội dung của sách. Vui lòng tham vấn bác sĩ chuyên khoa để được tư vấn trực tiếp.";

    /// <summary>
    /// Trả thông điệp từ chối khi câu hỏi chứa cụm từ chỉ định y tế bị chặn;
    /// <c>null</c> khi được phép trả lời. Câu hỏi rỗng không bị chặn (việc kiểm tra
    /// đầu vào thuộc use case).
    /// </summary>
    public string? FindRefusal(string? question)
    {
        if (string.IsNullOrWhiteSpace(question))
        {
            return null;
        }

        var normalized = Normalize(question);
        return ContainsBlockedPhrase(normalized) ? RefusalMessage : null;
    }

    private static bool ContainsBlockedPhrase(string normalized)
    {
        foreach (var phrase in MedicalAdvicePhrases.Blocked)
        {
            if (ContainsPhrase(normalized, phrase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsPhrase(string text, string phrase) =>
        string.Concat(" ", text, " ").Contains(
            string.Concat(" ", phrase, " "),
            StringComparison.Ordinal);

    private static string Normalize(string question)
    {
        var unaccented = Unaccent(question.ToLowerInvariant());
        return NonWord().Replace(unaccented, " ").Trim();
    }

    private static string Unaccent(string text)
    {
        var decomposed = text.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);

        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            builder.Append(character == 'đ' ? 'd' : character);
        }

        return builder.ToString();
    }

    [GeneratedRegex(@"[^a-z0-9]+", RegexOptions.CultureInvariant)]
    private static partial Regex NonWord();
}
