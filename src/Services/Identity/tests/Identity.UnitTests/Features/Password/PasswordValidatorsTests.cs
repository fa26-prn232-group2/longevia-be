using Identity.Application.Features.Password;

namespace Identity.UnitTests.Features.Password;

public sealed class PasswordValidatorsTests
{
    [Theory]
    [InlineData("Abcdef1!", true)]
    [InlineData("abcdefgh1!", false)]
    [InlineData("ABCDEFGH1!", false)]
    [InlineData("Abcdefgh!", false)]
    [InlineData("Abc1!", false)]
    public void ResetPasswordValidator_EnforcesRegistrationPasswordPolicy(string password, bool isValid)
    {
        var validator = new ResetPasswordRequestValidator();

        var result = validator.Validate(new ResetPasswordRequest("reset-token", password));

        Assert.Equal(isValid, result.IsValid);
    }

    [Theory]
    [InlineData("Abcdef1!", true)]
    [InlineData("abcdefgh1!", false)]
    public void ChangePasswordValidator_UsesSamePasswordPolicy(string password, bool isValid)
    {
        var validator = new ChangePasswordRequestValidator();

        var result = validator.Validate(new ChangePasswordRequest("current-password", password));

        Assert.Equal(isValid, result.IsValid);
    }
}
