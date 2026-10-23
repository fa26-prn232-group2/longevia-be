using Recommendation.Application.Features.MealPlans;
using Recommendation.Application.Features.Plans;
using Recommendation.Domain.Models;

namespace Recommendation.UnitTests.Features.MealPlans;

/// <summary>
/// T029 — D-06: prompt chỉ chứa candidate đã lọc, ngân sách ngày/bữa và corpus
/// chương 4 + Phụ lục A; AI chỉ được chọn <c>food_index</c> + citation, không
/// được viết rationale hay số liệu.
/// </summary>
public sealed class MealPlanPromptTests
{
    private readonly MealPlanPromptBuilder _builder = new(new PlanPromptFormatter());

    [Fact]
    public void IncludesFilteredCandidatesAndBudgets()
    {
        var request = Request();
        var budget = new WeeklyBudgetSplitter(new PlanDateRange()).Split(request);
        var candidates = new[] { Food("f-rice", "Cơm gạo lứt"), Food("f-tofu", "Đậu hũ") };

        var prompt = _builder.Build(request, candidates, budget, []);

        Assert.Contains("f-rice", prompt.UserPrompt, StringComparison.Ordinal);
        Assert.Contains("Cơm gạo lứt", prompt.UserPrompt, StringComparison.Ordinal);
        Assert.Contains("f-tofu", prompt.UserPrompt, StringComparison.Ordinal);
        Assert.Contains("2026-10-26", prompt.UserPrompt, StringComparison.Ordinal);
        Assert.Contains("BREAKFAST", prompt.UserPrompt, StringComparison.Ordinal);
        Assert.Contains("LUNCH", prompt.UserPrompt, StringComparison.Ordinal);
        Assert.Contains("DINNER", prompt.UserPrompt, StringComparison.Ordinal);
        Assert.Contains("SNACK", prompt.UserPrompt, StringComparison.Ordinal);
    }

    [Fact]
    public void IncludesChapterFourAndAppendixCorpusOnly()
    {
        var request = Request();
        var budget = new WeeklyBudgetSplitter(new PlanDateRange()).Split(request);

        var prompt = _builder.Build(
            request,
            [Food("f-rice", "Cơm gạo lứt")],
            budget,
            [
                Chunk(4, 72, "Nội dung chương bốn về chế độ ăn."),
                Chunk(12, 238, "Nội dung Phụ lục A về thực đơn hai tuần."),
                Chunk(12, 237, "Nội dung chương mười hai trước phụ lục."),
            ]);

        Assert.Equal(StructuredResponseKind.MealPlanSelection, prompt.ResponseKind);
        Assert.Contains(PlanPromptFormatter.CorpusBeginMarker, prompt.UserPrompt, StringComparison.Ordinal);
        Assert.Contains("Nội dung chương bốn về chế độ ăn.", prompt.UserPrompt, StringComparison.Ordinal);
        Assert.Contains("Nội dung Phụ lục A về thực đơn hai tuần.", prompt.UserPrompt, StringComparison.Ordinal);
        Assert.DoesNotContain("Nội dung chương mười hai trước phụ lục.", prompt.UserPrompt, StringComparison.Ordinal);
    }

    [Fact]
    public void DoesNotAskTheModelForRationaleOrNutritionNumbers()
    {
        var request = Request();
        var budget = new WeeklyBudgetSplitter(new PlanDateRange()).Split(request);

        var prompt = _builder.Build(request, [Food("f-rice", "Cơm gạo lứt")], budget, []);

        Assert.Contains("Không viết rationale", prompt.SystemInstruction, StringComparison.Ordinal);
        Assert.Contains("Chỉ chọn", prompt.SystemInstruction, StringComparison.Ordinal);
        Assert.Contains("food_index", prompt.SystemInstruction, StringComparison.Ordinal);
        Assert.Contains("không nêu số liệu", prompt.SystemInstruction, StringComparison.Ordinal);
    }

    private static GenerateMealPlanRequest Request() => new()
    {
        Profile = new UserProfile
        {
            Age = 35,
            Sex = "FEMALE",
            HeightCm = 165,
            WeightKg = 60,
            ActivityLevel = "ModeratelyActive",
            DietaryPreference = "None",
            Allergies = [],
        },
        TargetCalories = 2000,
        TargetProteinGrams = 100,
        TargetCarbsGrams = 250,
        TargetFatGrams = 67,
        TargetFiberGrams = 30,
        WeekStartDate = "2026-10-26",
        Days = 1,
        MealsPerDay = 4,
        CandidateFoods = [],
        Locale = "vi-VN",
    };

    private static CandidateFood Food(string foodId, string name) => new()
    {
        FoodId = foodId,
        Name = name,
        ServingSize = "1",
        ServingUnit = "phần",
        Calories = 200,
        ProteinGrams = 10,
        CarbsGrams = 25,
        FatGrams = 6,
        FiberGrams = 3,
        Allergens = [],
    };

    private static RetrievedChunk Chunk(int chapter, int page, string content) => new()
    {
        Chunk = new KnowledgeChunk
        {
            CorpusId = Guid.NewGuid(),
            ChapterNumber = chapter,
            ChapterTitle = $"Chương {chapter}",
            Content = content,
            BookPageFrom = page,
            BookPageTo = page,
            PdfPageFrom = page,
            PdfPageTo = page,
            Verified = true,
            SourceFile = $"chapter-{chapter}.md",
        },
        Score = 0.9,
    };
}
