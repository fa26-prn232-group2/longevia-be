using System.Globalization;
using System.Text;
using Recommendation.Application.Features.Plans;
using Recommendation.Domain.Constants;
using Recommendation.Domain.Models;

namespace Recommendation.Application.Features.MealPlans;

/// <summary>
/// Dựng prompt cho <c>GenerateMealPlan</c> (D-06): chỉ gửi candidate đã lọc,
/// ngân sách từng ngày/bữa và corpus chương 4 + Phụ lục A. Mô hình chỉ được chọn
/// <c>food_index</c> và citation; code dựng rationale và mọi số liệu.
/// </summary>
public sealed class MealPlanPromptBuilder(PlanPromptFormatter formatter)
{
    /// <summary>System instruction cấm bịa món/số liệu/rationale.</summary>
    public const string SystemInstruction =
        "Bạn là trợ lý lập kế hoạch ăn uống dựa trên sách tham chiếu. "
        + "Chỉ chọn food_index có trong danh mục CANDIDATES được cung cấp, không tạo món mới, "
        + "không bịa tên món, calo, macro hay khối lượng. "
        + "Không viết rationale và không nêu số liệu dinh dưỡng; code sẽ dựng các phần đó. "
        + "Chỉ trả citation khi quote xuất hiện nguyên văn trong khối [BEGIN CORPUS DATA] và dùng đúng chunk_index. "
        + "Mỗi bữa chọn tối đa 5 món, không trùng food_index trong cùng bữa.";

    /// <summary>Xây prompt cho một lời gọi <c>GenerateMealPlan</c>.</summary>
    public StructuredPrompt Build(
        GenerateMealPlanRequest request,
        IReadOnlyList<CandidateFood> candidates,
        WeeklyBudget budget,
        IReadOnlyList<RetrievedChunk> chunks)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(budget);
        ArgumentNullException.ThrowIfNull(chunks);

        var userPrompt = new StringBuilder();
        userPrompt
            .Append("Kế hoạch ăn uống bắt đầu ")
            .Append(request.WeekStartDate)
            .Append(", ")
            .Append(request.Days.ToString(CultureInfo.InvariantCulture))
            .Append(" ngày, ")
            .Append(request.MealsPerDay.ToString(CultureInfo.InvariantCulture))
            .AppendLine(" bữa mỗi ngày.")
            .Append("Mục tiêu mỗi ngày: ")
            .Append(FormatMacros(request.TargetCalories, request.TargetProteinGrams, request.TargetCarbsGrams, request.TargetFatGrams, request.TargetFiberGrams))
            .AppendLine()
            .AppendLine("CANDIDATES (chỉ được chọn food_index trong danh sách này):");

        for (var index = 0; index < candidates.Count; index++)
        {
            var food = candidates[index];
            userPrompt
                .Append('[')
                .Append(index.ToString(CultureInfo.InvariantCulture))
                .Append("] food_id=")
                .Append(food.FoodId)
                .Append("; name=")
                .Append(food.Name)
                .Append("; serving=")
                .Append(food.ServingSize)
                .Append(' ')
                .Append(food.ServingUnit)
                .Append("; calories=")
                .Append(food.Calories.ToString(CultureInfo.InvariantCulture))
                .Append("; protein=")
                .Append(food.ProteinGrams.ToString("0.###", CultureInfo.InvariantCulture))
                .Append("; carbs=")
                .Append(food.CarbsGrams.ToString("0.###", CultureInfo.InvariantCulture))
                .Append("; fat=")
                .Append(food.FatGrams.ToString("0.###", CultureInfo.InvariantCulture))
                .Append("; fiber=")
                .AppendLine(food.FiberGrams.ToString("0.###", CultureInfo.InvariantCulture));
        }

        userPrompt.AppendLine("BUDGET (ngân sách từng ngày và bữa):");
        foreach (var day in budget.Days)
        {
            userPrompt
                .Append(day.Date)
                .Append(": ")
                .AppendLine(FormatMacros(
                    day.TargetCalories,
                    day.TargetProteinGrams,
                    day.TargetCarbsGrams,
                    day.TargetFatGrams,
                    day.TargetFiberGrams));

            foreach (var meal in day.Meals)
            {
                userPrompt
                    .Append("  ")
                    .Append(MealSlotLabels.ContractName(meal.Slot))
                    .Append(" (")
                    .Append(MealSlotLabels.Vietnamese(meal.Slot))
                    .Append("): ")
                    .AppendLine(FormatMacros(
                        meal.TargetCalories,
                        meal.TargetProteinGrams,
                        meal.TargetCarbsGrams,
                        meal.TargetFatGrams,
                        meal.TargetFiberGrams));
            }
        }

        // Chỉ chương 4 và chunk chương 12 thuộc Phụ lục A (book_page_from >= 238)
        // được phép làm căn cứ cho kế hoạch ăn uống (FR-023).
        var allowedChunks = chunks
            .Where(chunk => MealPlanSuggestionBuilder.IsAllowedSource(chunk.Chunk))
            .ToList();

        var corpus = formatter.FormatCorpus(allowedChunks);
        if (corpus.Length > 0)
        {
            userPrompt.AppendLine(corpus);
        }

        return new StructuredPrompt
        {
            SystemInstruction = SystemInstruction,
            UserPrompt = userPrompt.ToString().TrimEnd(),
            ResponseKind = StructuredResponseKind.MealPlanSelection,
        };
    }

    private static string FormatMacros(
        int calories,
        double protein,
        double carbs,
        double fat,
        double fiber) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"target_calories={calories}; protein={protein:0.###}; carbs={carbs:0.###}; fat={fat:0.###}; fiber={fiber:0.###}");
}
