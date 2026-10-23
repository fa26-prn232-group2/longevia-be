using System.Globalization;
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
            if (!IsAllowedSource(chunk))
            {
                continue;
            }

            var excerpt = BuildExcerpt(chunk.Content);
            if (excerpt.Length == 0)
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

    private static string BuildExcerpt(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return string.Empty;
        }

        var collapsed = Whitespace().Replace(content, " ").Trim();
        var words = collapsed.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        return words.Length <= MaxWords
            ? collapsed
            : string.Join(' ', words.Take(MaxWords));
    }

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex Whitespace();
}

/// <summary>Kết quả dựng suggestion cho ngày chạm trần.</summary>
public sealed record MealPlanSuggestionBuildResult
{
    /// <summary>Gợi ý đã verify; rỗng khi không có nguồn.</summary>
    public required IReadOnlyList<PlanSuggestion> Suggestions { get; init; }

    /// <summary>Warning scope ngày khi không có nguồn suggestion.</summary>
    public required IReadOnlyList<PlanWarning> Warnings { get; init; }
}
