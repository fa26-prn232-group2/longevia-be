using System.Globalization;
using System.Text.Json.Nodes;
using Recommendation.Application.Abstractions;
using Recommendation.Application.Features.Ask;
using Recommendation.Application.Features.Meals;
using Recommendation.Domain.Constants;
using Recommendation.Domain.Models;

namespace Recommendation.Application.Features.NutrientGaps;

/// <summary>
/// Use case phân tích thiếu hụt vi chất (FR-008..FR-011): code tính khoảng thiếu
/// deterministic → truy xuất sách → mô hình diễn giải và chọn thực phẩm từ
/// CONTEXT → code kiểm chứng trích dẫn, lọc dị ứng và đối chiếu thực phẩm với
/// nguồn. Corpus rỗng hoặc provider lỗi thì chỉ trả phần tính toán, không bịa.
/// </summary>
public sealed class AnalyzeNutrientGapsUseCase(
    NutrientGapCalculator calculator,
    IKnowledgeStore knowledgeStore,
    PromptBuilder promptBuilder,
    IChatModel chatModel,
    CitationResolver citationResolver,
    AllergenGuard allergenGuard)
{
    private const int MaxSuggestedFoods = 10;

    /// <summary>Chạy toàn bộ pipeline phân tích vi chất cho một lời gọi.</summary>
    public async Task<NutrientGapResult> ExecuteAsync(
        NutrientGapRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var gaps = calculator.Calculate(request.Profile, request.Past7Days, request.Targets);
        if (gaps.Count == 0)
        {
            return ComputedOnly(gaps, request);
        }

        var chunks = await RetrieveAsync(request.Profile, gaps, cancellationToken).ConfigureAwait(false);
        if (chunks.Count == 0)
        {
            return ComputedOnly(gaps, request);
        }

        var prompt = promptBuilder.BuildNutrientGapAnalysis(request.Profile, gaps, chunks);
        var completion = await chatModel.CompleteAsync(prompt, cancellationToken).ConfigureAwait(false);
        if (completion is not { Abstained: false, Payload: not null })
        {
            return ComputedOnly(gaps, request);
        }

        var summary = ReadSummary(completion.Payload);
        var resolution = citationResolver.Resolve(completion.Payload, chunks);
        if (summary.Length == 0 || resolution.Abstained)
        {
            return ComputedOnly(gaps, request);
        }

        return new NutrientGapResult
        {
            Gaps = gaps,
            DeficientNutrients = gaps.Select(gap => gap.Nutrient).ToList(),
            SuggestedFoods = FilterFoods(ReadFoods(completion.Payload), chunks, request.Profile),
            AnalysisSummary = summary,
            Citations = resolution.Citations,
            Disclaimer = DisclaimerText.Value,
        };
    }

    private Task<IReadOnlyList<RetrievedChunk>> RetrieveAsync(
        UserProfile profile,
        IReadOnlyList<NutrientGap> gaps,
        CancellationToken cancellationToken)
    {
        var labels = gaps.Select(gap => LabelFor(gap.Nutrient, profile));
        return knowledgeStore.SearchAsync(
            new KnowledgeQuery { Query = "Thực phẩm bổ sung " + string.Join(", ", labels) },
            cancellationToken);
    }

    private IReadOnlyList<string> FilterFoods(
        IReadOnlyList<string> foods,
        IReadOnlyList<RetrievedChunk> chunks,
        UserProfile profile)
    {
        var grounded = foods
            .Where(food => chunks.Any(chunk => AllergenAliases.ContainsPhrase(chunk.Chunk.Content, food)))
            .Select(food => new CandidateFood { FoodId = food, Name = food })
            .ToList();

        return allergenGuard.Filter(grounded, profile.Allergies)
            .Select(food => food.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(MaxSuggestedFoods)
            .ToList();
    }

    private static NutrientGapResult ComputedOnly(
        IReadOnlyList<NutrientGap> gaps,
        NutrientGapRequest request)
    {
        return new NutrientGapResult
        {
            Gaps = gaps,
            DeficientNutrients = gaps.Select(gap => gap.Nutrient).ToList(),
            AnalysisSummary = BuildSummary(gaps, request),
            Disclaimer = DisclaimerText.Value,
        };
    }

    private static string BuildSummary(IReadOnlyList<NutrientGap> gaps, NutrientGapRequest request)
    {
        if (request.Past7Days.Count == 0)
        {
            return "Chưa có dữ liệu dinh dưỡng trong 7 ngày nên chưa thể phân tích thiếu hụt vi chất.";
        }

        if (gaps.Count == 0)
        {
            return "Không phát hiện thiếu hụt vi chất nào trong 7 ngày qua so với nhu cầu tham chiếu.";
        }

        var parts = gaps.Select(gap => string.Create(
            CultureInfo.InvariantCulture,
            $"{LabelFor(gap.Nutrient, request.Profile)} (mức {SeverityLabel(gap.Severity)}, thiếu {gap.Deficit:0.##} {gap.Unit})"));

        return $"Trong {request.Past7Days.Count} ngày qua bạn thiếu {gaps.Count} dưỡng chất: "
            + string.Join("; ", parts) + ".";
    }

    private static string LabelFor(string nutrient, UserProfile profile) =>
        MicronutrientRda.TryGet(nutrient, profile.Age, profile.Sex, out var reference)
            ? reference!.Label
            : nutrient;

    private static string SeverityLabel(string severity) => severity switch
    {
        NutrientGapCalculator.Severe => "nặng",
        NutrientGapCalculator.Moderate => "trung bình",
        _ => "nhẹ",
    };

    private static string ReadSummary(JsonObject payload)
    {
        if (payload["analysis_summary"] is JsonValue value
            && value.TryGetValue<string>(out var text)
            && !string.IsNullOrWhiteSpace(text))
        {
            return text.Trim();
        }

        return string.Empty;
    }

    private static IReadOnlyList<string> ReadFoods(JsonObject payload)
    {
        if (payload["suggested_foods"] is not JsonArray array)
        {
            return [];
        }

        var foods = new List<string>(array.Count);
        foreach (var node in array)
        {
            if (node is JsonValue value
                && value.TryGetValue<string>(out var text)
                && !string.IsNullOrWhiteSpace(text))
            {
                foods.Add(text.Trim());
            }
        }

        return foods;
    }
}
