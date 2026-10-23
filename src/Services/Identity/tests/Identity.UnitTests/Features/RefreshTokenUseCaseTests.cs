using Identity.Application.Features.Auth;
using Identity.Domain.Entities;
using Identity.UnitTests.Common;
using Longevia.Shared.Errors;

namespace Identity.UnitTests.Features;

public sealed class RefreshTokenUseCaseTests
{
    private readonly InMemoryUserRepository _userRepository = new();
    private readonly InMemoryRefreshTokenRepository _refreshTokenRepository = new();
    private readonly FakeJwtTokenGenerator _jwtTokenGenerator = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly RefreshTokenUseCase _sut;

    public RefreshTokenUseCaseTests()
    {
        _sut = new RefreshTokenUseCase(
            _userRepository,
            _refreshTokenRepository,
            _jwtTokenGenerator,
            _unitOfWork);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldRotateTokens_WhenTokenIsActive()
    {
        var user = User.Create(
            Guid.NewGuid(),
            "alex@example.com",
            "hashed",
            "Alex",
            DateTimeOffset.UtcNow,
            "token");
        await _userRepository.AddAsync(user);

        var existingToken = RefreshToken.Create(
            Guid.NewGuid(),
            user.Id,
            "valid_refresh_token",
            DateTimeOffset.UtcNow.AddDays(7),
            DateTimeOffset.UtcNow);
        await _refreshTokenRepository.AddAsync(existingToken);

        var request = new RefreshTokenRequest("valid_refresh_token");
        var result = await _sut.ExecuteAsync(request);

        Assert.NotNull(result);
        Assert.Equal($"fake_jwt_for_{user.Id}", result.AccessToken);
        Assert.Equal("fake_refresh_token", result.RefreshToken);

        Assert.True(existingToken.IsRevoked);
        Assert.Equal("fake_refresh_token", existingToken.ReplacedByToken);
        Assert.Equal(1, _unitOfWork.SaveChangesCalls);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldThrowUnauthorized_WhenTokenIsRevoked()
    {
        var user = User.Create(Guid.NewGuid(), "alex@example.com", "hashed", "Alex", DateTimeOffset.UtcNow, "token");
        await _userRepository.AddAsync(user);

        var revokedToken = RefreshToken.Create(
            Guid.NewGuid(),
            user.Id,
            "revoked_token",
            DateTimeOffset.UtcNow.AddDays(7),
            DateTimeOffset.UtcNow);
        revokedToken.Revoke(DateTimeOffset.UtcNow);
        await _refreshTokenRepository.AddAsync(revokedToken);

        var request = new RefreshTokenRequest("revoked_token");
        var ex = await Assert.ThrowsAsync<AppException>(() => _sut.ExecuteAsync(request));

        Assert.Equal(401, ex.Status);
        Assert.Equal("identity.invalid_refresh_token", ex.Code);
    }
}
