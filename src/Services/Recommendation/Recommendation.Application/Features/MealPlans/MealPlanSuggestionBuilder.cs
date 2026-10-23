using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Recommendation.Domain.Constants;
using Recommendation.Domain.Models;

namespace Recommendation.Application.Features.MealPlans;

/// <summary>
/// Dựng <c>suggestions</c> cho ngày chạm trần 105% (FR-017/FR-023): chỉ dùng quote
/// nguyên văn từ chương 4 hoặc chunk chương 12 có <c>book_page_from &gt;= 238</c>
/// (Phụ lục A), tối đa 3 gợi ý. Không có nguồn thì trả rỗng kèm warning
/// <c>suggestion_source_missing</c>; suggestion không mang <c>food_id</c>.
/// </summary>
public sealed partial class MealPlanSuggestionBuilder
{
    /// <summary>Chương chứa Phụ lục A (thực đơn hai tuần).</summary>
    public const int AppendixChapterNumber = 12;

    /// <summary>Trang bản in bắt đầu của Phụ lục A.</summary>
    public const int AppendixMinBookPage = 238;

    private const int MaxWords = 40;

    // FR-017: suggestion chỉ dành cho đồ uống 0 calo/thói quen thư giãn. Không
    // dùng từ khoá chung "nước"/"uống" một mình vì chúng xuất hiện cả trong
    // ngữ cảnh nấu ăn. So khớp sau khi bỏ dấu, không phân biệt hoa thường.
    private static readonly string[] SuggestionKeywords =
    [
        "trà",
        "thảo mộc",
        "nước lọc",
        "nước không calo",
        "đồ uống không calo",
        "0 calo",
        "thư giãn",
        "giấc ngủ",
        "ngủ đủ",
        "nghỉ ngơi",
    ];

    // Ngữ cảnh nấu ăn/bổ sung: nếu cửa sổ excerpt quanh keyword rơi vào các
    // ngữ cảnh này thì bỏ chunk để suggestion không bị hiểu thành công thức
    // hoặc hướng dẫn bổ sung.
    private static readonly string[] ExcludedContextKeywords =
    [
        "nồi",
        "dun sôi",
        "nấu",
        "nguyên liệu",
        "muỗng",
        "thìa",
        "gram",
        "ml",
        "vitamin",
        "viên",
        "bổ sung",
    ];

    /// <summary>Dựng suggestion cho một ngày chạm trần; warning mang scope của ngày.</summary>
    public MealPlanSuggestionBuildResult Build(string date, IReadOnlyList<RetrievedChunk> chunks)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(date);
        ArgumentNullException.ThrowIfNull(chunks);

        var suggestions = new List<PlanSuggestion>(PlanDefaults.MaxSuggestionsPerDay);

        foreach (var retrieved in chunks)
        {
            if (suggestions.Count >= PlanDefaults.MaxSuggestionsPerDay)
            {
                break;
            }

            var chunk = retrieved.Chunk;
            if (!IsAllowedSource(chunk) || !TryBuildExcerpt(chunk.Content, out var excerpt))
            {
                continue;
            }

            suggestions.Add(new PlanSuggestion
            {
                Text = string.Create(
                    CultureInfo.InvariantCulture,
                    $"Gợi ý từ sách (chương {chunk.ChapterNumber}, trang {chunk.BookPageFrom}): “{excerpt}”"),
                Citations =
                [
                    new Citation
                    {
                        ChapterNumber = chunk.ChapterNumber,
                        ChapterTitle = chunk.ChapterTitle ?? string.Empty,
                        BookPage = chunk.BookPageFrom,
                        PdfPage = chunk.PdfPageFrom,
                        Quote = excerpt,
                        Verified = chunk.Verified,
                        SourceFile = chunk.SourceFile ?? string.Empty,
                    },
                ],
            });
        }

        var warnings = new List<PlanWarning>(suggestions.Count == 0 ? 1 : 0);
        if (suggestions.Count == 0)
        {
            warnings.Add(new PlanWarning
            {
                Code = PlanWarningCodes.SuggestionSourceMissing,
                Message = "Không có đoạn sách phù hợp để gợi ý cho ngày đã chạm trần calo.",
                Scope = date,
            });
        }

        return new MealPlanSuggestionBuildResult
        {
            Suggestions = suggestions,
            Warnings = warnings,
        };
    }

    /// <summary><c>true</c> khi chunk thuộc chương 4 hoặc Phụ lục A của chương 12.</summary>
    public static bool IsAllowedSource(KnowledgeChunk chunk)
    {
        ArgumentNullException.ThrowIfNull(chunk);

        return chunk.ChapterNumber == 4
            || (chunk.ChapterNumber == AppendixChapterNumber && chunk.BookPageFrom >= AppendixMinBookPage);
    }

    private static bool TryBuildExcerpt(string content, out string excerpt)
    {
        excerpt = string.Empty;
        if (string.IsNullOrWhiteSpace(content))
        {
            return false;
        }

        var collapsed = Whitespace().Replace(content, " ").Trim();
        var words = collapsed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0)
        {
            return false;
        }

        var flatTokens = new List<(string Token, int WordIndex, string Original)>();
        for (var wordIndex = 0; wordIndex < words.Length; wordIndex++)
        {
            foreach (var token in Tokenize(words[wordIndex]))
            {
                flatTokens.Add((token, wordIndex, words[wordIndex]));
            }
        }

        foreach (var keyword in SuggestionKeywords)
        {
            var needle = Tokenize(keyword);
            if (needle.Count == 0 || FindSequence(flatTokens, needle) is not { } matchIndex)
            {
                continue;
            }

            var firstWord = flatTokens[matchIndex].WordIndex;
            var lastWord = flatTokens[matchIndex + needle.Count - 1].WordIndex;
            var span = lastWord - firstWord + 1;
            var start = Math.Max(0, firstWord - Math.Max(0, (MaxWords - span) / 2));
            var end = Math.Min(words.Length, start + MaxWords);
            start = Math.Max(0, end - MaxWords);

            excerpt = string.Join(' ', words[start..end]);
            return !ContainsExcludedContext(excerpt);
        }

        return false;
    }

    private static bool ContainsExcludedContext(string excerpt)
    {
        var tokens = Tokenize(excerpt);

        foreach (var keyword in ExcludedContextKeywords)
        {
            var needle = Tokenize(keyword);
            if (needle.Count > 0 && ContainsSequence(tokens, needle))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsSequence(
        IReadOnlyList<string> tokens,
        IReadOnlyList<string> needle)
    {
        for (var start = 0; start + needle.Count <= tokens.Count; start++)
        {
            var matched = true;
            for (var offset = 0; offset < needle.Count; offset++)
            {
                if (!string.Equals(tokens[start + offset], needle[offset], StringComparison.Ordinal))
                {
                    matched = false;
                    break;
                }
            }

            if (matched)
            {
                return true;
            }
        }

        return false;
    }

    private static int? FindSequence(
        IReadOnlyList<(string Token, int WordIndex, string Original)> flatTokens,
        IReadOnlyList<string> needle)
    {
        for (var start = 0; start + needle.Count <= flatTokens.Count; start++)
        {
            var matched = true;
            for (var offset = 0; offset < needle.Count; offset++)
            {
                var token = flatTokens[start + offset];
                if (!string.Equals(token.Token, needle[offset], StringComparison.Ordinal)
                    || IsAmbiguousFalsePositive(needle[offset], token.Original))
                {
                    matched = false;
                    break;
                }
            }

            if (matched)
            {
                return start;
            }
        }

        return null;
    }

    // Sau khi bỏ dấu, "ngủ" và "ngũ" đều thành "ngu"; loại "ngũ" để chunk về
    // ngũ cốc không bị coi là nội dung về giấc ngủ.
    private static bool IsAmbiguousFalsePositive(string token, string originalWord) =>
        token == "ngu"
        && originalWord.ToLowerInvariant().StartsWith("ngũ", StringComparison.Ordinal);

    private static List<string> Tokenize(string text) =>
        WordSeparator()
            .Split(RemoveDiacritics(text.ToLowerInvariant()))
            .Where(token => token.Length > 0)
            .ToList();

    private static string RemoveDiacritics(string text)
    {
        var decomposed = text.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);

        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(character == 'đ' ? 'd' : character);
            }
        }

        return builder.ToString();
    }

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex Whitespace();

    [GeneratedRegex(@"[^\p{L}\p{Nd}]+", RegexOptions.CultureInvariant)]
    private static partial Regex WordSeparator();
}

/// <summary>Kết quả dựng suggestion cho ngày chạm trần.</summary>
public sealed record MealPlanSuggestionBuildResult
{
    /// <summary>Gợi ý đã verify; rỗng khi không có nguồn.</summary>
    public required IReadOnlyList<PlanSuggestion> Suggestions { get; init; }

    /// <summary>Warning scope ngày khi không có nguồn suggestion.</summary>
    public required IReadOnlyList<PlanWarning> Warnings { get; init; }
}
