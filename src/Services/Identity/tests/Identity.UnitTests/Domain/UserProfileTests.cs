using Identity.Domain.Constants;
using Identity.Domain.Entities;

namespace Identity.UnitTests.Domain;

public sealed class UserProfileTests
{
    [Fact]
    public void Create_ShouldPreserveFractionalHeightAndNormalizeAllergies()
    {
        var profile = UserProfile.Create(
            Guid.NewGuid(), Guid.NewGuid(), new DateOnly(1995, 5, 20), BiologicalSex.Female,
            175.55m, ActivityLevel.LightlyActive, DietaryPreference.Vegan,
            [" Seafood ", "Seafood", " ", "Peanuts"], DateTimeOffset.UtcNow);

        Assert.Equal(175.55m, profile.HeightCm);
        Assert.Equal(["Seafood", "Peanuts"], profile.Allergies);
    }

    [Theory]
    [InlineData("49.99")]
    [InlineData("250.01")]
    public void Create_ShouldRejectHeightOutsideSupportedRange(string rawHeight)
    {
        var height = decimal.Parse(rawHeight, System.Globalization.CultureInfo.InvariantCulture);

        Assert.Throws<ArgumentOutOfRangeException>(() => UserProfile.Create(
            Guid.NewGuid(), Guid.NewGuid(), new DateOnly(1995, 5, 20), BiologicalSex.Male,
            height, ActivityLevel.Sedentary, DietaryPreference.None, [], DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Create_ShouldRejectHeightWithMoreThanTwoDecimalPlaces()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => UserProfile.Create(
            Guid.NewGuid(), Guid.NewGuid(), new DateOnly(1995, 5, 20), BiologicalSex.Male,
            175.555m, ActivityLevel.Sedentary, DietaryPreference.None, [], DateTimeOffset.UtcNow));
    }
}
