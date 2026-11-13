using System.Security.Cryptography;
using System.Text;
using Identity.Application.Features.Registration;
using Identity.Domain.Entities;
using Identity.Domain.Repositories;
using Identity.UnitTests.Common;
using Longevia.Shared.Errors;
using Longevia.Shared.Events;

namespace Identity.UnitTests.Features;

public sealed class RegisterUserUseCaseTests
{
    private readonly InMemoryUserRepository _userRepository = new();
    private readonly FakePasswordHasher _passwordHasher = new();
    private readonly FakeEventPublisher _eventPublisher = new();
    private readonly InMemoryPendingRegistrationStore _pendingStore = new();
    private readonly RegisterUserUseCase _sut;

    public RegisterUserUseCaseTests()
    {
        _sut = new RegisterUserUseCase(
            _userRepository,
            _passwordHasher,
            _eventPublisher,
            _pendingStore);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldStorePendingAndPublishEventWithoutDatabaseUser()
    {
        var request = new RegisterUserRequest("alex.minh@example.com", "Password123!", "Alex Minh");

        var pending = await _sut.ExecuteAsync(request);

        Assert.Equal("alex.minh@example.com", pending.Email);
        Assert.Equal("Alex Minh", pending.FullName);
        Assert.Empty(_userRepository.Users);
        Assert.Single(_pendingStore.Records);
        Assert.Single(_eventPublisher.PublishedEvents);
        var registeredEvent = Assert.IsType<UserRegisteredEvent>(_eventPublisher.PublishedEvents[0]);
        Assert.Equal(pending.Id, registeredEvent.UserId);
        Assert.Equal(64, registeredEvent.VerificationToken.Length);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(registeredEvent.VerificationToken))),
            pending.VerificationTokenHash);
        Assert.InRange(pending.ExpiresAt - pending.CreatedAt,
            PendingRegistration.Lifetime, PendingRegistration.Lifetime);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldOverwritePending_WhenEmailRegistersAgain()
    {
        var first = await _sut.ExecuteAsync(
            new RegisterUserRequest("same@example.com", "Password123!", "First Name"));
        var second = await _sut.ExecuteAsync(
            new RegisterUserRequest("same@example.com", "OtherPassword123!", "Second Name"));

        Assert.NotEqual(first.Id, second.Id);
        Assert.Single(_pendingStore.Records);
        Assert.Equal("Second Name", second.FullName);
        Assert.Null(await _pendingStore.GetByTokenHashAsync(first.VerificationTokenHash));
        Assert.NotNull(await _pendingStore.GetByTokenHashAsync(second.VerificationTokenHash));
    }

    [Fact]
    public async Task ExecuteAsync_ShouldThrowConflict_WhenVerifiedUserExists()
    {
        var existing = User.Create(Guid.NewGuid(), "existing@example.com", "hash", "Existing",
            DateTimeOffset.UtcNow, "token-hash", DateTimeOffset.UtcNow.AddHours(1));
        Assert.True(existing.TryVerify("token-hash", DateTimeOffset.UtcNow));
        await _userRepository.AddAsync(existing);

        var ex = await Assert.ThrowsAsync<AppException>(() =>
            _sut.ExecuteAsync(new RegisterUserRequest("existing@example.com", "Password123!", "New")));

        Assert.Equal(409, ex.Status);
        Assert.Equal("identity.email_taken", ex.Code);
        Assert.Empty(_pendingStore.Records);
    }
}
