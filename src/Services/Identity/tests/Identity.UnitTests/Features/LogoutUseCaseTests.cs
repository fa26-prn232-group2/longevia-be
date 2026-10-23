using Identity.Application.Features.Auth;
using Identity.Domain.Entities;
using Identity.UnitTests.Common;
using Longevia.Shared.Errors;

namespace Identity.UnitTests.Features;

public sealed class LogoutUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_ShouldRevokeRefreshToken_WhenTokenBelongsToCurrentUser()
    {
        var currentUser = new FakeCurrentUser { UserId = Guid.NewGuid() };
        var repository = new InMemoryRefreshTokenRepository();
        var unitOfWork = new FakeUnitOfWork();
        var token = RefreshToken.Create(
            Guid.NewGuid(), currentUser.UserId.Value, "owned-token",
            DateTimeOffset.UtcNow.AddDays(7), DateTimeOffset.UtcNow);
        await repository.AddAsync(token);

        var sut = new LogoutUseCase(currentUser, repository, unitOfWork);
        await sut.ExecuteAsync(new LogoutRequest("owned-token"));

        Assert.True(token.IsRevoked);
        Assert.Equal(1, unitOfWork.SaveChangesCalls);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldNotRevokeRefreshToken_WhenTokenBelongsToAnotherUser()
    {
        var currentUser = new FakeCurrentUser { UserId = Guid.NewGuid() };
        var repository = new InMemoryRefreshTokenRepository();
        var unitOfWork = new FakeUnitOfWork();
        var token = RefreshToken.Create(
            Guid.NewGuid(), Guid.NewGuid(), "other-users-token",
            DateTimeOffset.UtcNow.AddDays(7), DateTimeOffset.UtcNow);
        await repository.AddAsync(token);

        var sut = new LogoutUseCase(currentUser, repository, unitOfWork);
        var ex = await Assert.ThrowsAsync<AppException>(() =>
            sut.ExecuteAsync(new LogoutRequest("other-users-token")));

        Assert.Equal(401, ex.Status);
        Assert.Equal("identity.invalid_refresh_token", ex.Code);
        Assert.False(token.IsRevoked);
        Assert.Equal(0, unitOfWork.SaveChangesCalls);
    }
}
