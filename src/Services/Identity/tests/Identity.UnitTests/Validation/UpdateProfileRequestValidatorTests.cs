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
            175.5m,
            ActivityLevel.ModeratelyActive,
            DietaryPreference.Mediterranean,
            ["Peanuts"]);

        var result = _validator.Validate(request);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_ShouldPass_WhenUserIsExactly13YearsOldToday()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var request = ValidRequest(today.AddYears(-13));

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

    public static TheoryData<string, bool> HeightCases => new()
    {
        { "50", true },
        { "50.01", true },
        { "175.5", true },
        { "175.55", true },
        { "175.555", false },
        { "250", true },
        { "49.99", false },
        { "250.01", false },
    };

    [Theory]
    [MemberData(nameof(HeightCases))]
    public void Validate_ShouldApplyInclusiveDecimalHeightBounds(string rawHeight, bool expectedValid)
    {
        var request = ValidRequest(height: decimal.Parse(rawHeight, System.Globalization.CultureInfo.InvariantCulture));

        var result = _validator.Validate(request);

        Assert.Equal(expectedValid, result.IsValid);
        Assert.Equal(expectedValid, !result.Errors.Any(e => e.PropertyName == nameof(UpdateProfileRequest.HeightCm)));
    }

    [Theory]
    [InlineData("sex", "Unknown", "BiologicalSex")]
    [InlineData("activity", "Running", "ActivityLevel")]
    [InlineData("diet", "Carnivore", "DietaryPreference")]
    public void Validate_ShouldRejectUnknownEnumValues(string field, string value, string propertyName)
    {
        var request = ValidRequest() with
        {
            BiologicalSex = field == "sex" ? value : BiologicalSex.Male,
            ActivityLevel = field == "activity" ? value : ActivityLevel.Sedentary,
            DietaryPreference = field == "diet" ? value : DietaryPreference.None,
        };

        var result = _validator.Validate(request);

        Assert.Contains(result.Errors, error => error.PropertyName == propertyName);
    }

    private static UpdateProfileRequest ValidRequest(DateOnly? birthDate = null, decimal height = 175.5m) => new(
        birthDate ?? new DateOnly(1995, 5, 20),
        BiologicalSex.Male,
        height,
        ActivityLevel.Sedentary,
        DietaryPreference.None,
        []);
}
