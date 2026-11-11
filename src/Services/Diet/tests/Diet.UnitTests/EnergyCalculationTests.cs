using Diet.Domain;

namespace Diet.UnitTests;

public sealed class EnergyCalculationTests
{
    private static EnergyCalculation Create(bool isMale, bool isOther) =>
        new(80m, DateTimeOffset.UtcNow.AddDays(-1), Guid.NewGuid(), DateTimeOffset.UtcNow,
            180m, 30, isMale, 1.55m, DateTimeOffset.UtcNow, isOther);

    [Fact]
    public void OtherSex_UsesNeutralAdjustmentBetweenMaleAndFemale()
    {
        var male = Create(true, false);
        var female = Create(false, false);
        var other = Create(false, true);

        Assert.True(other.Bmr < male.Bmr);
        Assert.True(other.Bmr > female.Bmr);
        Assert.Equal((male.Bmr + female.Bmr) / 2m, other.Bmr, 2);
    }

    [Fact]
    public void MaleAndFemale_KeepExistingFormula()
    {
        // BMR nam = 10*80 + 6.25*180 - 5*30 + 5 = 1780; nữ = 1614.
        Assert.Equal(1780m, Create(true, false).Bmr);
        Assert.Equal(1614m, Create(false, false).Bmr);
    }
}
