using Recommendation.Application.Features.NutrientGaps;
using Recommendation.Domain.Models;

namespace Recommendation.UnitTests.Features.NutrientGaps;

/// <summary>
/// T092 — FR-009/FR-010: lượng thiếu tính trên trung bình 7 ngày, phân mức theo
/// biên chính xác 25% và 50%, và không sinh khoảng thiếu khi đã đủ nhu cầu.
/// </summary>
public sealed class NutrientGapCalculatorTests
{
    private static readonly UserProfile AdultFemale = new()
    {
        Age = 30,
        Sex = "FEMALE",
    };

    [Theory]
    [InlineData(75.1, "mild")]
    [InlineData(75.0, "moderate")]
    [InlineData(50.0, "moderate")]
    [InlineData(49.9, "severe")]
    public void SeverityUsesTheExactBoundaries(double fiberPerDay, string expectedSeverity)
    {
        var gaps = Calculate(
            Days(fiberPerDay),
            new NutrientTargets { TargetFiberGrams = 100 });

        var gap = Assert.Single(gaps);
        Assert.Equal("Fiber", gap.Nutrient);
        Assert.Equal(expectedSeverity, gap.Severity);
    }

    [Fact]
    public void DoesNotReportGapWhenIntakeMeetsTheTarget()
    {
        var gaps = Calculate(
            Days(120),
            new NutrientTargets { TargetFiberGrams = 100 });

        Assert.Empty(gaps);
    }

    [Fact]
    public void AveragesIntakeAcrossTheProvidedDays()
    {
        var days = new List<DailyNutrientSummary>
        {
            Day(fiber: 30),
            Day(fiber: 60),
            Day(fiber: 90),
        };

        var gaps = Calculate(days, new NutrientTargets { TargetFiberGrams = 100 });

        var gap = Assert.Single(gaps);
        Assert.Equal(60, gap.AverageIntake, 3);
        Assert.Equal(40, gap.Deficit, 3);
        Assert.Equal("moderate", gap.Severity);
    }

    [Fact]
    public void EmptyWeekProducesNoGaps()
    {
        var gaps = Calculate([], new NutrientTargets { TargetFiberGrams = 100 });

        Assert.Empty(gaps);
    }

    [Fact]
    public void FallsBackToTheReferenceTableWhenNoTargetIsProvided()
    {
        var gaps = Calculate(
            Days(10),
            new NutrientTargets { TargetFiberGrams = 0 });

        var gap = Assert.Single(gaps);
        Assert.Equal("Fiber", gap.Nutrient);

        // Nữ 19–30 tuổi: 25 g/ngày theo bảng tham chiếu.
        Assert.Equal(25, gap.Target, 3);
        Assert.Equal(15, gap.Deficit, 3);
        Assert.Equal("severe", gap.Severity);
    }

    [Fact]
    public void ReportsMicronutrientGapFromCallerIntakeAndRdaTarget()
    {
        var days = DaysWithMicronutrients(
            25,
            new MicronutrientIntake { Nutrient = "Vitamin D", Unit = "mcg", Amount = 5 });

        var gap = Assert.Single(Calculate(days, new NutrientTargets()));

        // Nữ 19–30 tuổi: nhu cầu Vitamin D là 15 µg/ngày.
        Assert.Equal("Vitamin D", gap.Nutrient);
        Assert.Equal("µg", gap.Unit);
        Assert.Equal(15, gap.Target, 3);
        Assert.Equal(5, gap.AverageIntake, 3);
        Assert.Equal(10, gap.Deficit, 3);
        Assert.Equal("severe", gap.Severity);
    }

    [Fact]
    public void DoesNotInventMicronutrientGapsWhenCallerSendsNone()
    {
        var gaps = Calculate(Days(25), new NutrientTargets());

        Assert.Empty(gaps);
    }

    [Fact]
    public void IgnoresMicronutrientsThatAreNotInTheReferenceTable()
    {
        var days = DaysWithMicronutrients(
            25,
            new MicronutrientIntake { Nutrient = "Unobtainium", Unit = "mg", Amount = 100 });

        var gaps = Calculate(days, new NutrientTargets());

        Assert.Empty(gaps);
    }

    [Fact]
    public void MatchesNutrientNamesIgnoringCaseAndExtraWhitespace()
    {
        var days = DaysWithMicronutrients(
            25,
            new MicronutrientIntake { Nutrient = "  vitamin   d ", Unit = "µg", Amount = 5 });

        var gap = Assert.Single(Calculate(days, new NutrientTargets()));

        Assert.Equal("Vitamin D", gap.Nutrient);
        Assert.Equal(10, gap.Deficit, 3);
    }

    [Fact]
    public void ConvertsCallerAmountIntoTheReferenceUnit()
    {
        // 0,005 mg = 5 µg — cùng họ khối lượng nên quy đổi an toàn.
        var days = DaysWithMicronutrients(
            25,
            new MicronutrientIntake { Nutrient = "Vitamin D", Unit = "mg", Amount = 0.005 });

        var gap = Assert.Single(Calculate(days, new NutrientTargets()));

        Assert.Equal(5, gap.AverageIntake, 3);
        Assert.Equal(10, gap.Deficit, 3);
    }

    [Fact]
    public void SkipsMicronutrientWhenTheUnitCannotBeConverted()
    {
        // IU phụ thuộc từng vi chất nên không quy đổi mù; bỏ qua thay vì bịa.
        var days = DaysWithMicronutrients(
            25,
            new MicronutrientIntake { Nutrient = "Vitamin D", Unit = "IU", Amount = 200 });

        var gaps = Calculate(days, new NutrientTargets());

        Assert.Empty(gaps);
    }

    [Fact]
    public void AveragesOnlyOverDaysThatReportedTheMicronutrient()
    {
        // 3/7 ngày có dữ liệu: không coi 4 ngày còn lại là "nạp 0".
        var days = Enumerable.Range(0, 7).Select(day => day < 3
            ? Day(25) with
            {
                Micronutrients =
                [
                    new MicronutrientIntake { Nutrient = "Vitamin D", Unit = "µg", Amount = 5 },
                ],
            }
            : Day(25)).ToList();

        var gap = Assert.Single(Calculate(days, new NutrientTargets()));

        Assert.Equal(5, gap.AverageIntake, 3);
        Assert.Equal(10, gap.Deficit, 3);
    }

    private static IReadOnlyList<NutrientGap> Calculate(
        IReadOnlyList<DailyNutrientSummary> days,
        NutrientTargets targets) =>
        new NutrientGapCalculator().Calculate(AdultFemale, days, targets);

    private static IReadOnlyList<DailyNutrientSummary> DaysWithMicronutrients(
        double fiberPerDay,
        params MicronutrientIntake[] intakes) =>
        Enumerable.Range(0, 7)
            .Select(_ => Day(fiberPerDay) with { Micronutrients = intakes })
            .ToList();

    private static IReadOnlyList<DailyNutrientSummary> Days(double fiberPerDay, int count = 7) =>
        Enumerable.Range(0, count).Select(_ => Day(fiberPerDay)).ToList();

    private static DailyNutrientSummary Day(double fiber) => new()
    {
        Date = "2026-10-22",
        Calories = 2000,
        Protein = 46,
        Carbs = 130,
        Fat = 67,
        Fiber = fiber,
    };
}
