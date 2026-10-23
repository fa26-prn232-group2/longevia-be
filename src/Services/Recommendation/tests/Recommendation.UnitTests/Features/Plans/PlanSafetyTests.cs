using Recommendation.Application.Features.Ask;
using Recommendation.Application.Features.Plans;
using Recommendation.Domain.Constants;
using Recommendation.Domain.Models;

namespace Recommendation.UnitTests.Features.Plans;

/// <summary>
/// T018 — SC-011/FR-011/FR-035: disclaimer luôn nêu rõ chỉ mang tính tham khảo,
/// rationale không chứa khuyến nghị thuốc/chẩn đoán/điều trị.
/// </summary>
public sealed class PlanSafetyTests
{
    private static readonly string[] MedicalAdviceTokens =
    [
        "chẩn đoán",
        "kê đơn",
        "liều thuốc",
        "điều trị",
        "chữa bệnh",
    ];

    private readonly PlanRationaleBuilder _builder = new(new SafetyGuard());

    [Fact]
    public void DisclaimerIsReferenceOnly()
    {
        Assert.Contains("tham khảo", DisclaimerText.Value, StringComparison.Ordinal);
        Assert.Contains("không thay thế", DisclaimerText.Value, StringComparison.Ordinal);
        Assert.DoesNotContain("chẩn đoán", DisclaimerText.Value, StringComparison.Ordinal);
    }

    [Fact]
    public void MealRationaleDoesNotOfferMedicalAdvice()
    {
        var rationale = _builder.BuildMealRationale(
            MealSlot.Lunch,
            [Food()],
            []);

        AssertNoMedicalAdvice(rationale);
    }

    [Fact]
    public void WorkoutRationaleDoesNotOfferMedicalAdvice()
    {
        var rationale = _builder.BuildWorkoutRationale("walking", 30, ["đau lưng"], []);

        AssertNoMedicalAdvice(rationale);
    }

    [Fact]
    public void UnsafeQuoteIsNotEchoedIntoRationale()
    {
        var rationale = _builder.BuildWorkoutRationale(
            "walking",
            30,
            [],
            [Citation("Hãy uống thuốc theo chỉ định để chữa bệnh nhanh hơn.")]);

        AssertNoMedicalAdvice(rationale);
        Assert.Contains("Theo sách", rationale, StringComparison.Ordinal);
    }

    private static void AssertNoMedicalAdvice(string text)
    {
        foreach (var token in MedicalAdviceTokens)
        {
            Assert.DoesNotContain(token, text, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static CandidateFood Food() => new()
    {
        FoodId = "food-1",
        Name = "Cơm gạo lứt",
        Calories = 200,
        ProteinGrams = 5,
        CarbsGrams = 40,
        FatGrams = 2,
        FiberGrams = 3,
        Allergens = [],
    };

    private static Citation Citation(string quote) => new()
    {
        ChapterNumber = 5,
        ChapterTitle = "TẬP THỂ DỤC",
        BookPage = 90,
        PdfPage = 82,
        Quote = quote,
        Verified = true,
        SourceFile = "05-chuong-5-tap-the-duc-va-song-lau-khoe-manh.md",
    };
}
