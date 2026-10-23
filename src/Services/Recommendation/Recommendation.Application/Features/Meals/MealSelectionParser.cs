using System.Globalization;
using System.Text.Json.Nodes;
using Recommendation.Application.Features.Ask;
using Recommendation.Domain.Models;

namespace Recommendation.Application.Features.Meals;

/// <summary>
/// Chuyển payload JSON của mô hình thành <see cref="MealSuggestion"/>: chỉ số món
/// phải trỏ vào danh mục đã lọc, dinh dưỡng lấy từ món gốc (FR-001, FR-005), lý
/// do và trích dẫn do mô hình viết nhưng trích dẫn phải qua <see cref="CitationResolver"/>.
/// </summary>
public sealed class MealSelectionParser(CitationResolver citationResolver)
{
    /// <summary>Số món tối đa mỗi bữa (FR-007).</summary>
    public const int MaxMealsPerSlot = 5;

    /// <summary>Đọc tối đa <see cref="MaxMealsPerSlot"/> món; bỏ món trùng hoặc chỉ số không hợp lệ.</summary>
    public IReadOnlyList<MealSuggestion> Parse(
        JsonObject? payload,
        IReadOnlyList<CandidateFood> candidates,
        IReadOnlyList<RetrievedChunk> chunks)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(chunks);

        if (payload?["selections"] is not JsonArray selections)
        {
            return [];
        }

        var meals = new List<MealSuggestion>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var node in selections)
        {
            if (meals.Count >= MaxMealsPerSlot)
            {
                break;
            }

            var meal = ParseOne(node as JsonObject, candidates, chunks);
            if (meal is null || !seen.Add(meal.FoodId))
            {
                continue;
            }

            meals.Add(meal);
        }

        return meals;
    }

    private MealSuggestion? ParseOne(
        JsonObject? node,
        IReadOnlyList<CandidateFood> candidates,
        IReadOnlyList<RetrievedChunk> chunks)
    {
        if (node is null || !TryReadIndex(node, out var index) || index < 0 || index >= candidates.Count)
        {
            return null;
        }

        var food = candidates[index];

        return new MealSuggestion
        {
            FoodId = food.FoodId,
            Name = food.Name,
            EstimatedCalories = food.Calories,
            ProteinGrams = food.ProteinGrams,
            CarbsGrams = food.CarbsGrams,
            FatGrams = food.FatGrams,
            Rationale = ReadRationale(node, food),
            Citations = citationResolver.Resolve(node, chunks).Citations,
        };
    }

    private static string ReadRationale(JsonObject node, CandidateFood food)
    {
        if (node["rationale"] is JsonValue value
            && value.TryGetValue<string>(out var text)
            && !string.IsNullOrWhiteSpace(text))
        {
            return text.Trim();
        }

        return string.Create(
            CultureInfo.InvariantCulture,
            $"Bổ sung {food.ProteinGrams:0.#} g protein và {food.Calories} kcal theo danh mục đã gửi.");
    }

    private static bool TryReadIndex(JsonObject node, out int index)
    {
        index = -1;

        return node["food_index"] is JsonValue value
            && value.TryGetValue<int>(out index);
    }
}
