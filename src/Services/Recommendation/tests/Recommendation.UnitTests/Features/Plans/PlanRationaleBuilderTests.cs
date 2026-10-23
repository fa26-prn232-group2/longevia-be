using Recommendation.Application.Features.Ask;
using Recommendation.Application.Features.Plans;
using Recommendation.Domain.Constants;
using Recommendation.Domain.Models;

namespace Recommendation.UnitTests.Features.Plans;

/// <summary>
/// T018 — rationale do code dựng từ request + món/hoạt động + quote đã verify;
/// không nhận free text của AI và không chứa khuyến nghị y tế.
/// </summary>
public sealed class PlanRationaleBuilderTests
{
    private readonly PlanRationaleBuilder _builder = new(new SafetyGuard());

    [Fact]
    public void MealRationaleUsesSelectedFoodsAndVerifiedQuote()
    {
        var rationale = _builder.BuildMealRationale(
            MealSlot.Lunch,
            [Food("Cơm gạo lứt", 200, 5), Food("Cá hồi", 150, 10)],
            [Citation("Chất béo không bão hòa tốt cho tim mạch.")]);

        Assert.Contains("Bữa trưa", rationale, StringComparison.Ordinal);
        Assert.Contains("2 món", rationale, StringComparison.Ordinal);
        Assert.Contains("350 kcal", rationale, StringComparison.Ordinal);
        Assert.Contains("15 g protein", rationale, StringComparison.Ordinal);
        Assert.Contains("Chất béo không bão hòa tốt cho tim mạch.", rationale, StringComparison.Ordinal);
    }

    [Fact]
    public void MealRationaleWithoutCitationsStillUsesRequestData()
    {
        var rationale = _builder.BuildMealRationale(
            MealSlot.Breakfast,
            [Food("Yến mạch", 120, 4)],
            []);

        Assert.Contains("Bữa sáng", rationale, StringComparison.Ordinal);
        Assert.Contains("120 kcal", rationale, StringComparison.Ordinal);
        Assert.DoesNotContain("Theo sách", rationale, StringComparison.Ordinal);
    }

    [Fact]
    public void WorkoutRationaleUsesActivityDurationAndLimitations()
    {
        var rationale = _builder.BuildWorkoutRationale(
            "walking",
            30,
            ["đau lưng"],
            [Citation("Đi bộ là hoạt động nhẹ nhàng, an toàn cho nhiều người.")]);

        Assert.Contains("walking", rationale, StringComparison.Ordinal);
        Assert.Contains("30 phút", rationale, StringComparison.Ordinal);
        Assert.Contains("đau lưng", rationale, StringComparison.Ordinal);
        Assert.Contains("Đi bộ là hoạt động nhẹ nhàng", rationale, StringComparison.Ordinal);
    }

    [Fact]
    public void WorkoutRationaleWithoutLimitationsStatesAssumption()
    {
        var rationale = _builder.BuildWorkoutRationale("walking", 30, [], []);

        Assert.Contains("chưa khai giới hạn sức khỏe", rationale, StringComparison.Ordinal);
        Assert.DoesNotContain("khỏe mạnh", rationale, StringComparison.Ordinal);
        Assert.DoesNotContain("chấn thương", rationale, StringComparison.Ordinal);
    }

    [Fact]
    public void RationaleOmitsUnsafeMedicalQuote()
    {
        var rationale = _builder.BuildMealRationale(
            MealSlot.Dinner,
            [Food("Rau củ", 100, 3)],
            [Citation("Bạn nên uống thuốc mỗi ngày để điều trị bệnh.")]);

        Assert.Contains("Theo sách", rationale, StringComparison.Ordinal);
        Assert.DoesNotContain("uống thuốc", rationale, StringComparison.Ordinal);
        Assert.DoesNotContain("điều trị", rationale, StringComparison.Ordinal);
    }

    private static CandidateFood Food(string name, int calories, double protein) => new()
    {
        FoodId = name,
        Name = name,
        ServingSize = "1",
        ServingUnit = "phần",
        Calories = calories,
        ProteinGrams = protein,
        CarbsGrams = 10,
        FatGrams = 3,
        FiberGrams = 2,
        Allergens = [],
    };

    private static Citation Citation(string quote) => new()
    {
        ChapterNumber = 4,
        ChapterTitle = "CHẾ ĐỘ ĂN TRƯỜNG THỌ",
        BookPage = 72,
        PdfPage = 64,
        Quote = quote,
        Verified = true,
        SourceFile = "04-chuong-4-che-do-an-truong-tho.md",
    };
}
