using Recommendation.Domain.Constants;

namespace Recommendation.UnitTests.Features.NutrientGaps;

/// <summary>
/// T091 — FR-008/FR-009: bảng nhu cầu tham chiếu chọn đúng nhóm tuổi và giới,
/// nam khác nữ ở sắt/canxi/năng lượng, và hồ sơ thiếu dữ liệu vẫn trả giá trị
/// thay vì ném lỗi.
/// </summary>
public sealed class MicronutrientRdaTests
{
    [Theory]
    [InlineData(25)]
    [InlineData(45)]
    public void IronForFemaleIsHigherThanMale(int age)
    {
        var male = MicronutrientRda.Get("Iron", age, "MALE");
        var female = MicronutrientRda.Get("Iron", age, "FEMALE");

        Assert.True(female.Target > male.Target);
        Assert.Equal("mg", female.Unit);
    }

    [Fact]
    public void CalciumForOlderFemaleIsHigherThanMale()
    {
        var male = MicronutrientRda.Get("Calcium", 60, "MALE");
        var female = MicronutrientRda.Get("Calcium", 60, "FEMALE");

        Assert.True(female.Target > male.Target);
        Assert.Equal("mg", female.Unit);
    }

    [Fact]
    public void EnergyForMaleIsHigherThanFemale()
    {
        var male = MicronutrientRda.Get("Energy", 30, "MALE");
        var female = MicronutrientRda.Get("Energy", 30, "FEMALE");

        Assert.True(male.Target > female.Target);
        Assert.Equal("kcal", male.Unit);
    }

    [Theory]
    [InlineData(25, 38)]
    [InlineData(60, 30)]
    public void FiberUsesTheAgeBand(int age, double expectedTarget)
    {
        var reference = MicronutrientRda.Get("Fiber", age, "MALE");

        Assert.Equal(expectedTarget, reference.Target, 3);
        Assert.Equal("g", reference.Unit);
    }

    [Fact]
    public void UnknownAgeAndSexFallBackToTheHighestRequirement()
    {
        var unknown = MicronutrientRda.Get("Iron", 0, "UNSPECIFIED");
        var female = MicronutrientRda.Get("Iron", 25, "FEMALE");
        var male = MicronutrientRda.Get("Iron", 25, "MALE");

        Assert.Equal(Math.Max(female.Target, male.Target), unknown.Target, 3);
    }

    [Fact]
    public void NegativeAgeDoesNotThrow()
    {
        var reference = MicronutrientRda.Get("Fiber", -5, string.Empty);

        Assert.True(reference.Target > 0);
    }

    [Fact]
    public void TryGetReturnsFalseForUnknownNutrient()
    {
        var found = MicronutrientRda.TryGet("Unobtainium", 30, "MALE", out var reference);

        Assert.False(found);
        Assert.Null(reference);
    }

    [Fact]
    public void TableCoversTheRequiredMicronutrients()
    {
        var required = new[]
        {
            "Vitamin A", "Vitamin C", "Vitamin D", "Vitamin E", "Vitamin K",
            "Vitamin B1", "Vitamin B2", "Vitamin B3", "Vitamin B5", "Vitamin B6",
            "Vitamin B9", "Vitamin B12", "Calcium", "Iron", "Magnesium", "Zinc",
            "Iodine", "Selenium", "Omega-3", "Fiber",
        };

        foreach (var nutrient in required)
        {
            Assert.Contains(nutrient, MicronutrientRda.Nutrients);
        }

        Assert.True(MicronutrientRda.Nutrients.Count >= 20);

        foreach (var nutrient in MicronutrientRda.Nutrients)
        {
            var reference = MicronutrientRda.Get(nutrient, 30, "MALE");
            Assert.False(string.IsNullOrWhiteSpace(reference.Unit));
            Assert.True(reference.Target > 0);
        }
    }
}
