using Diet.Application.Features.Targets;
using Diet.Domain;
using Diet.Domain.Entities;

namespace Diet.UnitTests;

public sealed class TargetValidationTests
{
    [Theory]
    [InlineData(237.5, true)]
    [InlineData(262.5, true)]
    [InlineData(237.499, false)]
    [InlineData(262.501, false)]
    public void Validate_MacroEnergyAtFivePercentBoundary_UsesInclusiveLimit(decimal protein, bool valid)
    {
        var request = new UpdateTargetRequest(1000, protein, 0, 0);
        Assert.Equal(valid, new UpdateTargetRequestValidator().Validate(request).IsValid);
        if (valid)
        {
            Assert.Equal(1000, DailyDietTarget.Create(Guid.NewGuid(),
                new NutritionValues(1000, protein, 0, 0, 0), DateTimeOffset.UtcNow).TargetCalories);
        }
        else
        {
            Assert.Throws<ArgumentException>(() => DailyDietTarget.Create(Guid.NewGuid(),
                new NutritionValues(1000, protein, 0, 0, 0), DateTimeOffset.UtcNow));
        }
    }

    [Fact]
    public void Validate_ZeroCaloriesWithZeroMacros_Rejects()
    {
        Assert.False(new UpdateTargetRequestValidator().Validate(new UpdateTargetRequest(0, 0, 0, 0)).IsValid);
    }
}
