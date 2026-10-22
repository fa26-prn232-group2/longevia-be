using System.Globalization;
using System.Text.RegularExpressions;
using Recommendation.Application.Abstractions;
using Recommendation.Application.Features.Ask;
using Recommendation.Domain.Constants;
using Recommendation.Domain.Models;

namespace Recommendation.Application.Features.Meals;

/// <summary>
/// Chọn món trong danh mục đã lọc bằng mô hình (chỉ số món + lý do + trích dẫn)
/// và lấy mẹo trường thọ từ kho tri thức. Mọi số liệu dinh dưỡng vẫn lấy từ
/// danh mục — mô hình không được sinh món mới (FR-001, FR-005).
/// </summary>
public sealed partial class MealSelector(
    PromptBuilder promptBuilder,
    IChatModel chatModel,
    IKnowledgeStore knowledgeStore,
    MealSelectionParser parser)
{
    private const int ContextTopK = 8;
    private const int TipTopK = 1;
    private const int MaxQueryCandidates = 3;
    private const int MaxTipWords = 40;

    /// <summary>Chọn món trong danh mục đã lọc; trả <c>Abstained</c> khi mô hình lỗi hoặc không chọn được.</summary>
    public async Task<MealSelectionOutcome> SelectAsync(
        MealRecommendationRequest request,
        IReadOnlyList<CandidateFood> candidates,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(candidates);

        var chunks = await RetrieveAsync(BuildMealQuery(request, candidates), ContextTopK, cancellationToken)
            .ConfigureAwait(false);
        var prompt = promptBuilder.BuildMealSelection(request, candidates, chunks);
        var completion = await chatModel.CompleteAsync(prompt, cancellationToken).ConfigureAwait(false);

        if (completion is not { Abstained: false, Payload: not null })
        {
            return new MealSelectionOutcome
            {
                Abstained = true,
                AbstainReason = completion.AbstainReason ?? "provider_unavailable",
            };
        }

        var meals = parser.Parse(completion.Payload, candidates, chunks);

        return meals.Count > 0
            ? new MealSelectionOutcome { Meals = meals }
            : new MealSelectionOutcome { Abstained = true, AbstainReason = "empty_selection" };
    }

    /// <summary>Lấy mẹo trường thọ từ RAG; không có đoạn phù hợp thì dùng <paramref name="fallbackTip"/>.</summary>
    public async Task<LongevityTip> BuildTipAsync(
        MealRecommendationRequest request,
        string fallbackTip,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var chunks = await RetrieveAsync(BuildTipQuery(request), TipTopK, cancellationToken).ConfigureAwait(false);
        if (chunks.Count == 0)
        {
            return new LongevityTip { Text = fallbackTip };
        }

        var chunk = chunks[0].Chunk;
        var quote = BuildQuote(chunk.Content);
        if (quote.Length == 0)
        {
            return new LongevityTip { Text = fallbackTip };
        }

        return new LongevityTip
        {
            Text = quote,
            Citations =
            [
                new Citation
                {
                    ChapterNumber = chunk.ChapterNumber,
                    ChapterTitle = chunk.ChapterTitle ?? string.Empty,
                    BookPage = chunk.BookPageFrom,
                    PdfPage = chunk.PdfPageFrom,
                    Quote = quote,
                    Verified = chunk.Verified,
                    SourceFile = chunk.SourceFile ?? string.Empty,
                },
            ],
        };
    }

    private Task<IReadOnlyList<RetrievedChunk>> RetrieveAsync(
        string query,
        int topK,
        CancellationToken cancellationToken) =>
        knowledgeStore.SearchAsync(
            new KnowledgeQuery { Query = query, TopK = topK },
            cancellationToken);

    private static string BuildMealQuery(
        MealRecommendationRequest request,
        IReadOnlyList<CandidateFood> candidates)
    {
        var names = candidates.Take(MaxQueryCandidates).Select(candidate => candidate.Name);
        var joined = string.Join(' ', names);

        return string.Create(
            CultureInfo.InvariantCulture,
            $"{MealSlotLabels.Vietnamese(request.MealSlot)} chế độ ăn trường thọ {request.Goal} {joined}");
    }

    private static string BuildTipQuery(MealRecommendationRequest request) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"thói quen ăn uống trường thọ {MealSlotLabels.Vietnamese(request.MealSlot)} {request.Goal}");

    private static string BuildQuote(string content)
    {
        var collapsed = Whitespace().Replace(content, " ").Trim();
        var words = collapsed.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        return words.Length <= MaxTipWords
            ? collapsed
            : string.Join(' ', words.Take(MaxTipWords));
    }

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex Whitespace();
}
