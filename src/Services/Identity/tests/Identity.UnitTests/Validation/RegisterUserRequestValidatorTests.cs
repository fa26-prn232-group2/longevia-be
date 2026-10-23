using Identity.Application.Features.Registration;

namespace Identity.UnitTests.Validation;

public sealed class RegisterUserRequestValidatorTests
{
    private readonly RegisterUserRequestValidator _validator = new();

    [Fact]
    public void Validate_ShouldPass_WhenRequestIsValid()
    {
        var request = new RegisterUserRequest("user@example.com", "Password123!", "Nguyễn Văn A");

        var result = _validator.Validate(request);

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("invalid-email")]
    [InlineData("user@")]
    public void Validate_ShouldFail_WhenEmailIsInvalid(string email)
    {
        var request = new RegisterUserRequest(email, "Password123!", "Nguyễn Văn A");

        var result = _validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(RegisterUserRequest.Email));
    }

    [Theory]
    [InlineData("short1!")] // < 8 chars
    [InlineData("password123!")] // no uppercase
    [InlineData("PASSWORD123!")] // no lowercase
    [InlineData("PasswordNoDigit!")] // no digit
    [InlineData("Password123")] // no special char
    public void Validate_ShouldFail_WhenPasswordDoesNotMeetRequirements(string password)
    {
        var request = new RegisterUserRequest("user@example.com", password, "Nguyễn Văn A");

        var result = _validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(RegisterUserRequest.Password));
    }
}
