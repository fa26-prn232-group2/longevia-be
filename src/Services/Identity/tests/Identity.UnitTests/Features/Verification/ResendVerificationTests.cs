using System.Security.Cryptography;
using System.Text;
using Identity.Application.Features.Verification;
using Identity.Domain.Constants;
using Identity.Domain.Entities;
using Identity.UnitTests.Common;
using Longevia.Shared.Events;

namespace Identity.UnitTests.Features.Verification;

public sealed class ResendVerificationTests
{
    private readonly InMemoryUserRepository _users = new();
    private readonly FakeEventPublisher _events = new();
    private readonly FakeUnitOfWork _uow = new();

    private ResendVerificationUseCase CreateSut() => new(_users, _events, _uow);

    private static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    [Fact]
    public async Task PendingUser_GetsNewTokenAndEmailEvent()
    {
        var user = User.Create(Guid.NewGuid(), "pending@example.com", "hash", "Pending User",
            DateTimeOffset.UtcNow, "old-hash", DateTimeOffset.UtcNow.AddHours(1));
        await _users.AddAsync(user);

        await CreateSut().ExecuteAsync(new ResendVerificationRequest(" PENDING@example.com "));

        Assert.Equal(1, _uow.SaveChangesCalls);
        var published = Assert.IsType<UserRegisteredEvent>(Assert.Single(_events.PublishedEvents));
        Assert.Equal(user.Id, published.UserId);
        Assert.Equal("Pending User", published.FullName);
        Assert.NotEqual("old-hash", user.VerificationTokenHash);
        Assert.Equal(Hash(published.VerificationToken),
            user.VerificationTokenHash);
        Assert.True(user.VerificationTokenExpiresAt > DateTimeOffset.UtcNow.AddHours(23));
    }

    [Fact]
    public async Task UnknownEmail_IsSilentlyIgnored()
    {
        await CreateSut().ExecuteAsync(new ResendVerificationRequest("missing@example.com"));

        Assert.Empty(_events.PublishedEvents);
        Assert.Equal(0, _uow.SaveChangesCalls);
    }

    [Fact]
    public async Task ActiveUser_DoesNotGetAnotherVerificationEmail()
    {
        var user = User.Create(Guid.NewGuid(), "active@example.com", "hash", "Active User",
            DateTimeOffset.UtcNow, Hash("token"),
            DateTimeOffset.UtcNow.AddHours(1));
        await _users.AddAsync(user);
        user.TryVerify(Hash("token"), DateTimeOffset.UtcNow);
        Assert.Equal(UserStatus.Active, user.Status);

        await CreateSut().ExecuteAsync(new ResendVerificationRequest(user.Email));

        Assert.Empty(_events.PublishedEvents);
        Assert.Equal(0, _uow.SaveChangesCalls);
    }
}
