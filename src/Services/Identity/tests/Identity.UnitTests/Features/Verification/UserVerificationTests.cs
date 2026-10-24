using Identity.Application.Features.Verification;
using Identity.Domain.Entities;

namespace Identity.UnitTests.Features.Verification;

public sealed class UserVerificationTests
{
    [Fact]
    public void TryVerify_ShouldActivateAndConsumeToken_WhenHashMatchesAndTokenIsFresh()
    {
        var now = DateTimeOffset.UtcNow;
        var user = CreateUser("expected-hash", now.AddHours(24));

        var verified = user.TryVerify("expected-hash", now);

        Assert.True(verified);
        Assert.Equal("Active", user.Status);
        Assert.Equal(now, user.VerificationTokenConsumedAt);
        Assert.Equal(now, user.UpdatedAt);
    }

    [Theory]
    [InlineData("wrong-hash", 24, false)]
    [InlineData("expected-hash", -1, false)]
    public void TryVerify_ShouldRejectWrongOrExpiredToken(string tokenHash, int expiryHours, bool expected)
    {
        var now = DateTimeOffset.UtcNow;
        var user = CreateUser("expected-hash", now.AddHours(expiryHours));

        Assert.Equal(expected, user.TryVerify(tokenHash, now));
        Assert.Equal("PendingVerification", user.Status);
        Assert.Null(user.VerificationTokenConsumedAt);
    }

    [Fact]
    public void TryVerify_ShouldRejectReplay()
    {
        var now = DateTimeOffset.UtcNow;
        var user = CreateUser("expected-hash", now.AddHours(24));

        Assert.True(user.TryVerify("expected-hash", now));
        Assert.False(user.TryVerify("expected-hash", now.AddMinutes(1)));
    }

    private static User CreateUser(string hash, DateTimeOffset expiresAt) =>
        User.Create(Guid.NewGuid(), "verify@example.com", "password-hash", "Verify User", DateTimeOffset.UtcNow, hash, expiresAt);
}
