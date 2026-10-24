using Diet.Domain;

namespace Diet.UnitTests;

public sealed class NutritionValuesTests
{
    [Fact]
    public void ForQuantity_OneAndHalfServings_ScalesEveryNutrient()
    {
        var result = new NutritionValues(165, 31, 0, 3.6m, 2).ForQuantity(150, 100);

        Assert.Equal(new NutritionValues(247.5m, 46.5m, 0, 5.4m, 3), result);
    }

    [Theory]
    [InlineData(0, 100)]
    [InlineData(-1, 100)]
    [InlineData(100, 0)]
    [InlineData(100, -1)]
    public void ForQuantity_NonPositiveQuantityOrServing_Rejects(decimal quantity, decimal serving)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new NutritionValues(1, 1, 1, 1, 1).ForQuantity(quantity, serving));
    }

    [Fact]
    public void ForQuantity_FractionalServing_RoundsSnapshotToEightDecimalPlaces()
    {
        var result = new NutritionValues(1, 1, 1, 1, 1).ForQuantity(1, 3);

        Assert.Equal(0.33333333m, result.Calories);
    }
}
