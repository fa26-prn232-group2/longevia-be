using Identity.Application.Features.Registration;
using Identity.Application.Features.Verification;
using Identity.Domain.Constants;
using Identity.UnitTests.Common;
using Longevia.Shared.Errors;
using Longevia.Shared.Events;

namespace Identity.UnitTests.Features;

public sealed class RegisterUserUseCaseTests
{
    private readonly InMemoryUserRepository _userRepository = new();
    private readonly FakePasswordHasher _passwordHasher = new();
    private readonly FakeEventPublisher _eventPublisher = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly RegisterUserUseCase _sut;

    public RegisterUserUseCaseTests()
    {
        _sut = new RegisterUserUseCase(
            _userRepository,
            _passwordHasher,
            _eventPublisher,
            _unitOfWork);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldCreateUserAndPublishEvent_WhenValid()
    {
        var request = new RegisterUserRequest("alex.minh@example.com", "Password123!", "Alex Minh");

        var result = await _sut.ExecuteAsync(request);

        Assert.NotNull(result);
        Assert.Equal("alex.minh@example.com", result.Email);
        Assert.Equal("Alex Minh", result.FullName);
        Assert.Equal(UserStatus.PendingVerification, result.Status);

        Assert.True(await _userRepository.ExistsByEmailAsync(request.Email));
        Assert.Equal(1, _unitOfWork.SaveChangesCalls);
        Assert.Single(_eventPublisher.PublishedEvents);
        var registeredEvent = Assert.IsType<UserRegisteredEvent>(_eventPublisher.PublishedEvents[0]);
        Assert.Equal(64, registeredEvent.VerificationToken.Length);
        var user = Assert.Single(_userRepository.Users);
        Assert.NotEqual(registeredEvent.VerificationToken, user.VerificationTokenHash);
        Assert.Equal(64, user.VerificationTokenHash?.Length);
        Assert.InRange(user.VerificationTokenExpiresAt!.Value - user.CreatedAt, TimeSpan.FromHours(24), TimeSpan.FromHours(24));
    }

    [Fact]
    public async Task ExecuteAsync_ShouldThrowConflict_WhenEmailAlreadyExists()
    {
        var request = new RegisterUserRequest("existing@example.com", "Password123!", "Alex Minh");
        await _sut.ExecuteAsync(request);

        var ex = await Assert.ThrowsAsync<AppException>(() => _sut.ExecuteAsync(request));

        Assert.Equal(409, ex.Status);
        Assert.Equal("identity.email_taken", ex.Code);
    }
}
