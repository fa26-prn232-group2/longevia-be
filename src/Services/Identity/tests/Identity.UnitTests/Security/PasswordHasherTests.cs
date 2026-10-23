using Identity.Infrastructure.Security;

namespace Identity.UnitTests.Security;

public sealed class PasswordHasherTests
{
    private readonly PasswordHasher _sut = new();

    [Fact]
    public void Hash_ShouldProduceNonEmptyHashDifferentFromPassword()
    {
        var password = "SuperSecretPassword123!";

        var hash = _sut.Hash(password);

        Assert.False(string.IsNullOrWhiteSpace(hash));
        Assert.NotEqual(password, hash);
    }

    [Fact]
    public void Verify_ShouldReturnTrue_WhenPasswordMatches()
    {
        var password = "CorrectPassword456#";
        var hash = _sut.Hash(password);

        var isValid = _sut.Verify(password, hash);

        Assert.True(isValid);
    }

    [Fact]
    public void Verify_ShouldReturnFalse_WhenPasswordDoesNotMatch()
    {
        var password = "CorrectPassword456#";
        var wrongPassword = "WrongPassword789$";
        var hash = _sut.Hash(password);

        var isValid = _sut.Verify(wrongPassword, hash);

        Assert.False(isValid);
    }
}
