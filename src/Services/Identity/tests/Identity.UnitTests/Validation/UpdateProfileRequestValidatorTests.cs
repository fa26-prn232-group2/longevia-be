using Identity.Application.Features.Users;
using Identity.Domain.Constants;

namespace Identity.UnitTests.Validation;

public sealed class UpdateProfileRequestValidatorTests
{
    private readonly UpdateProfileRequestValidator _validator = new();

    [Fact]
    public void Validate_ShouldPass_WhenRequestIsValid()
    {
        var request = new UpdateProfileRequest(
            new DateOnly(2000, 1, 1),
            BiologicalSex.Male,
            175,
            ActivityLevel.ModeratelyActive,
            DietaryPreference.Mediterranean,
            ["Peanuts"]);

        var result = _validator.Validate(request);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_ShouldFail_WhenUserIsUnder13()
    {
        var under13Date = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-10));
        var request = new UpdateProfileRequest(
            under13Date,
            BiologicalSex.Female,
            160,
            ActivityLevel.LightlyActive,
            DietaryPreference.None,
            []);

        var result = _validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(UpdateProfileRequest.BirthDate));
    }

    [Theory]
    [InlineData(40)]
    [InlineData(260)]
    public void Validate_ShouldFail_WhenHeightIsOutOfRange(int height)
    {
        var request = new UpdateProfileRequest(
            new DateOnly(1995, 5, 20),
            BiologicalSex.Male,
            height,
            ActivityLevel.Sedentary,
            DietaryPreference.None,
            []);

        var result = _validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(UpdateProfileRequest.HeightCm));
    }
}
