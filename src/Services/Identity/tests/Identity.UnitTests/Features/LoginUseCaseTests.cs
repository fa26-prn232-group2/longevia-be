using Identity.Application.Features.Auth;
using Identity.Domain.Entities;
using Identity.UnitTests.Common;
using Longevia.Shared.Errors;

namespace Identity.UnitTests.Features;

public sealed class LoginUseCaseTests
{
    private readonly InMemoryUserRepository _userRepository = new();
    private readonly InMemoryRefreshTokenRepository _refreshTokenRepository = new();
    private readonly FakePasswordHasher _passwordHasher = new();
    private readonly FakeJwtTokenGenerator _jwtTokenGenerator = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly LoginUseCase _sut;

    public LoginUseCaseTests()
    {
        _sut = new LoginUseCase(
            _userRepository,
            _refreshTokenRepository,
            _passwordHasher,
            _jwtTokenGenerator,
            _unitOfWork);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnTokens_WhenCredentialsAreValid()
    {
        var password = "ValidPassword123!";
        var user = User.Create(
            Guid.NewGuid(),
            "test@example.com",
            _passwordHasher.Hash(password),
            "Test User",
            DateTimeOffset.UtcNow,
            "hash",
            DateTimeOffset.UtcNow.AddHours(24));
        Assert.True(user.TryVerify("hash", DateTimeOffset.UtcNow));
        await _userRepository.AddAsync(user);

        var request = new LoginRequest("test@example.com", password);
        var result = await _sut.ExecuteAsync(request);

        Assert.NotNull(result);
        Assert.Equal($"fake_jwt_for_{user.Id}", result.AccessToken);
        Assert.Equal("fake_refresh_token", result.RefreshToken);
        Assert.Equal("Bearer", result.TokenType);
        Assert.Equal(900, result.ExpiresIn);
        Assert.Equal(1, _unitOfWork.SaveChangesCalls);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldThrowUnauthorized_WhenPasswordIsIncorrect()
    {
        var user = User.Create(
            Guid.NewGuid(),
            "test@example.com",
            _passwordHasher.Hash("CorrectPassword123!"),
            "Test User",
            DateTimeOffset.UtcNow,
            "hash",
            DateTimeOffset.UtcNow.AddHours(24));
        await _userRepository.AddAsync(user);

        var request = new LoginRequest("test@example.com", "WrongPassword123!");
        var ex = await Assert.ThrowsAsync<AppException>(() => _sut.ExecuteAsync(request));

        Assert.Equal(401, ex.Status);
        Assert.Equal("identity.invalid_credentials", ex.Code);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldThrowUnauthorized_WhenUserNotFound()
    {
        var request = new LoginRequest("notfound@example.com", "Password123!");
        var ex = await Assert.ThrowsAsync<AppException>(() => _sut.ExecuteAsync(request));

        Assert.Equal(401, ex.Status);
        Assert.Equal("identity.invalid_credentials", ex.Code);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldThrowUnauthorized_WhenUserIsPendingVerification()
    {
        var password = "ValidPassword123!";
        var user = User.Create(
            Guid.NewGuid(),
            "pending@example.com",
            _passwordHasher.Hash(password),
            "Pending User",
            DateTimeOffset.UtcNow,
            "hash",
            DateTimeOffset.UtcNow.AddHours(24));
        await _userRepository.AddAsync(user);

        var ex = await Assert.ThrowsAsync<AppException>(() =>
            _sut.ExecuteAsync(new LoginRequest(user.Email, password)));

        Assert.Equal(401, ex.Status);
        Assert.Equal("identity.invalid_credentials", ex.Code);
        Assert.Equal(0, _unitOfWork.SaveChangesCalls);
    }
}
