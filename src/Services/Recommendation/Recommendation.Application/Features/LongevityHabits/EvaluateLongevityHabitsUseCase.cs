using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using Recommendation.Application.Abstractions;
using Recommendation.Application.Features.Ask;
using Recommendation.Domain.Constants;
using Recommendation.Domain.Models;

namespace Recommendation.Application.Features.LongevityHabits;

/// <summary>
/// Use case đánh giá thói quen trường thọ (FR-012..FR-014): code tính điểm và
/// breakdown deterministic → truy xuất sách → mô hình chỉ viết gợi ý hành động
/// dựa trên CONTEXT → code kiểm chứng trích dẫn. Corpus rỗng hoặc provider lỗi
/// thì trả gợi ý tĩnh từ <see cref="LongevityTips"/> thay vì bịa nội dung.
/// </summary>
public sealed class EvaluateLongevityHabitsUseCase(
    LongevityRubricCalculator calculator,
    IKnowledgeStore knowledgeStore,
    PromptBuilder promptBuilder,
    IChatModel chatModel,
    CitationResolver citationResolver)
{
    private const int MaxInsights = 3;

    /// <summary>Chạy toàn bộ pipeline đánh giá thói quen cho một lời gọi.</summary>
    public async Task<LongevityHabitResult> ExecuteAsync(
        LongevityHabitRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var score = calculator.Calculate(request);
        var chunks = await RetrieveAsync(score, cancellationToken).ConfigureAwait(false);
        if (chunks.Count == 0)
        {
            return StaticResult(score);
        }

        var prompt = promptBuilder.Build(BuildQuestion(score), chunks);
        var completion = await chatModel.CompleteAsync(prompt, cancellationToken).ConfigureAwait(false);
        if (completion is not { Abstained: false, Payload: not null })
        {
            return StaticResult(score);
        }

        var insights = ReadInsights(completion.Payload);
        var resolution = citationResolver.Resolve(completion.Payload, chunks);
        if (insights.Count == 0 || resolution.Abstained)
        {
            return StaticResult(score);
        }

        return new LongevityHabitResult
        {
            LongevityScore = score.Score,
            Components = score.Components,
            ActionableInsights = insights,
            Citations = resolution.Citations,
            Disclaimer = DisclaimerText.Value,
        };
    }

    private Task<IReadOnlyList<RetrievedChunk>> RetrieveAsync(
        LongevityHabitScore score,
        CancellationToken cancellationToken)
    {
        var labels = score.Components
            .OrderBy(component => component.Score)
            .ThenByDescending(component => component.Weight)
            .ThenBy(component => component.Key, StringComparer.Ordinal)
            .Select(component => LabelFor(component.Key));

        return knowledgeStore.SearchAsync(
            new KnowledgeQuery { Query = "Cải thiện thói quen trường thọ: " + string.Join(", ", labels) },
            cancellationToken);
    }

    private static LongevityHabitResult StaticResult(LongevityHabitScore score) => new()
    {
        LongevityScore = score.Score,
        Components = score.Components,
        ActionableInsights = StaticInsights(score),
        Citations = [],
        Disclaimer = DisclaimerText.Value,
    };

    /// <summary>
    /// Gợi ý tĩnh deterministic ưu tiên nhánh điểm thấp nhất; khi mọi nhánh đã
    /// đạt tối đa thì vẫn là lời khuyên duy trì thói quen của từng nhánh.
    /// </summary>
    private static IReadOnlyList<string> StaticInsights(LongevityHabitScore score)
    {
        var tips = score.Components
            .OrderBy(component => component.Score)
            .ThenByDescending(component => component.Weight)
            .ThenBy(component => component.Key, StringComparer.Ordinal)
            .Select(component => TipFor(component.Key))
            .Where(tip => tip.Length > 0)
            .Take(MaxInsights)
            .ToList();

        return tips.Count == 0 ? [LongevityTips.Default] : tips;
    }

    private static string BuildQuestion(LongevityHabitScore score)
    {
        var builder = new StringBuilder();
        builder.AppendLine("ĐÁNH GIÁ THÓI QUEN TRƯỜNG THỌ (số liệu do hệ thống tính, KHÔNG tự tính lại):");
        builder.AppendLine(string.Create(
            CultureInfo.InvariantCulture,
            $"- Điểm tổng: {score.Score}/100"));

        foreach (var component in score.Components)
        {
            builder.AppendLine(string.Create(
                CultureInfo.InvariantCulture,
                $"- {LabelFor(component.Key)} (trọng số {component.Weight}%): {component.Score}/100 — {component.Reason}"));
        }

        builder.AppendLine();
        builder.AppendLine("Hãy đề xuất tối đa 3 gợi ý hành động cụ thể, ưu tiên nhánh điểm thấp nhất, chỉ dựa trên CONTEXT.");
        builder.Append("Mỗi gợi ý nằm trên một dòng, không đánh số, không thêm số liệu ngoài CONTEXT.");
        return builder.ToString();
    }

    private static IReadOnlyList<string> ReadInsights(JsonObject payload)
    {
        if (payload["insights"] is JsonArray array)
        {
            var fromArray = new List<string>(array.Count);
            foreach (var node in array)
            {
                if (node is JsonValue value
                    && value.TryGetValue<string>(out var text)
                    && !string.IsNullOrWhiteSpace(text))
                {
                    fromArray.Add(text.Trim());
                }
            }

            if (fromArray.Count > 0)
            {
                return fromArray.Take(MaxInsights).ToList();
            }
        }

        return payload["answer"] is JsonValue answerValue
            && answerValue.TryGetValue<string>(out var answer)
            ? SplitInsights(answer)
            : [];
    }

    private static IReadOnlyList<string> SplitInsights(string answer)
    {
        if (string.IsNullOrWhiteSpace(answer))
        {
            return [];
        }

        return answer
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(TrimBullet)
            .Where(insight => insight.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .Take(MaxInsights)
            .ToList();
    }

    private static string TrimBullet(string line)
    {
        var trimmed = line.Trim();
        return trimmed.Length > 1 && trimmed[0] is '-' or '*' or '•'
            ? trimmed[1..].Trim()
            : trimmed;
    }

    private static string TipFor(string key) => key switch
    {
        LongevityRubric.FastingKey => LongevityTips.Fasting,
        LongevityRubric.SleepKey => LongevityTips.Sleep,
        LongevityRubric.HydrationKey => LongevityTips.Hydration,
        LongevityRubric.NutritionKey => LongevityTips.Nutrition,
        _ => string.Empty,
    };

    private static string LabelFor(string key) => key switch
    {
        LongevityRubric.FastingKey => "nhịn ăn gián đoạn",
        LongevityRubric.SleepKey => "giấc ngủ",
        LongevityRubric.HydrationKey => "uống đủ nước",
        LongevityRubric.NutritionKey => "dinh dưỡng",
        _ => key,
    };
}
