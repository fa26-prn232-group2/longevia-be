using System.Security.Cryptography;
using System.Text;
using Identity.Application.Common.Errors;
using Identity.Application.Features.Password;
using Identity.Domain.Constants;
using Identity.Domain.Entities;
using Identity.UnitTests.Common;
using Longevia.Shared.Errors;

namespace Identity.UnitTests.Features.Password;

public sealed class PasswordUseCaseTests
{
    [Fact]
    public async Task ChangePassword_ValidCurrentPassword_ChangesHashAndRevokesEveryRefreshToken()
    {
        var user = CreateActiveUser();
        var users = new InMemoryUserRepository();
        await users.AddAsync(user);
        var refreshTokens = new InMemoryRefreshTokenRepository();
        var first = CreateRefreshToken(user.Id, "refresh-1");
        var second = CreateRefreshToken(user.Id, "refresh-2");
        await refreshTokens.AddAsync(first);
        await refreshTokens.AddAsync(second);
        var currentUser = new FakeCurrentUser { UserId = user.Id };
        var unitOfWork = new FakeUnitOfWork();
        var useCase = new ChangePasswordUseCase(
            currentUser, users, refreshTokens, new FakePasswordHasher(), unitOfWork);

        await useCase.ExecuteAsync(new ChangePasswordRequest("OldPassword123!", "NewPassword456!"));

        Assert.Equal("hashed_NewPassword456!", user.PasswordHash);
        Assert.True(first.IsRevoked);
        Assert.True(second.IsRevoked);
        Assert.Equal(1, unitOfWork.SaveChangesCalls);
    }

    [Fact]
    public async Task ChangePassword_WrongCurrentPassword_RejectsWithoutSaving()
    {
        var user = CreateActiveUser();
        var users = new InMemoryUserRepository();
        await users.AddAsync(user);
        var unitOfWork = new FakeUnitOfWork();
        var useCase = new ChangePasswordUseCase(
            new FakeCurrentUser { UserId = user.Id }, users,
            new InMemoryRefreshTokenRepository(), new FakePasswordHasher(), unitOfWork);

        var error = await Assert.ThrowsAsync<AppException>(() =>
            useCase.ExecuteAsync(new ChangePasswordRequest("wrong", "NewPassword456!")));

        Assert.Equal("identity.invalid_current_password", error.Code);
        Assert.Equal(0, unitOfWork.SaveChangesCalls);
    }

    [Fact]
    public async Task ForgotPassword_UnknownEmail_ReturnsWithoutPublishingOrChangingDatabase()
    {
        var publisher = new FakeEventPublisher();
        var unitOfWork = new FakeUnitOfWork();
        var useCase = new ForgotPasswordUseCase(new InMemoryUserRepository(), publisher, unitOfWork);

        await useCase.ExecuteAsync(new ForgotPasswordRequest("missing@example.com"));

        Assert.Empty(publisher.PublishedEvents);
        Assert.Equal(0, unitOfWork.SaveChangesCalls);
    }

    [Fact]
    public async Task ForgotPassword_PendingAccount_ReturnsWithoutPublishing()
    {
        var user = User.Create(
            Guid.NewGuid(), "pending@example.com", "hashed_password", "Pending User",
            DateTimeOffset.UtcNow, "verification-hash", DateTimeOffset.UtcNow.AddHours(24));
        var users = new InMemoryUserRepository();
        await users.AddAsync(user);
        var publisher = new FakeEventPublisher();
        var unitOfWork = new FakeUnitOfWork();
        var useCase = new ForgotPasswordUseCase(users, publisher, unitOfWork);

        await useCase.ExecuteAsync(new ForgotPasswordRequest(user.Email));

        Assert.Empty(publisher.PublishedEvents);
        Assert.Null(user.PasswordResetTokenHash);
        Assert.Equal(0, unitOfWork.SaveChangesCalls);
    }

    [Fact]
    public async Task ForgotPassword_ActiveAccount_PublishesRawTokenButStoresOnlyHash()
    {
        var user = CreateActiveUser();
        var users = new InMemoryUserRepository();
        await users.AddAsync(user);
        var publisher = new FakeEventPublisher();
        var unitOfWork = new FakeUnitOfWork();
        var useCase = new ForgotPasswordUseCase(users, publisher, unitOfWork);
        var requestedAt = DateTimeOffset.UtcNow;

        await useCase.ExecuteAsync(new ForgotPasswordRequest(user.Email));

        var resetEvent = Assert.IsType<Longevia.Shared.Events.PasswordResetRequestedEvent>(
            Assert.Single(publisher.PublishedEvents));
        Assert.Equal(user.Email, resetEvent.Email);
        Assert.Equal(TimeSpan.FromMinutes(15), resetEvent.ExpiresAt - resetEvent.OccurredOn);
        Assert.Equal(Hash(resetEvent.ResetToken), user.PasswordResetTokenHash);
        Assert.NotEqual(resetEvent.ResetToken, user.PasswordResetTokenHash);
        Assert.InRange(resetEvent.OccurredOn, requestedAt, DateTimeOffset.UtcNow);
        Assert.Equal(1, unitOfWork.SaveChangesCalls);
    }

    [Fact]
    public async Task ResetPassword_ValidToken_ConsumesTokenChangesPasswordAndRevokesSessions()
    {
        const string token = "single-use-reset-token";
        var user = CreateActiveUser();
        user.IssuePasswordResetToken(Hash(token), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMinutes(15));
        var users = new InMemoryUserRepository();
        await users.AddAsync(user);
        var refreshTokens = new InMemoryRefreshTokenRepository();
        var refreshToken = CreateRefreshToken(user.Id, "old-refresh");
        await refreshTokens.AddAsync(refreshToken);
        var unitOfWork = new FakeUnitOfWork();
        var useCase = new ResetPasswordUseCase(users, refreshTokens, new FakePasswordHasher(), unitOfWork);

        await useCase.ExecuteAsync(new ResetPasswordRequest(token, "NewPassword456!"));

        Assert.Equal("hashed_NewPassword456!", user.PasswordHash);
        Assert.Equal("old-refresh", refreshToken.Token);
        Assert.True(refreshToken.IsRevoked);
        Assert.NotNull(user.PasswordResetTokenConsumedAt);
        Assert.Equal(1, unitOfWork.SaveChangesCalls);
    }

    [Fact]
    public async Task ResetPassword_ExpiredToken_RejectsWithoutSaving()
    {
        const string token = "expired-reset-token";
        var user = CreateActiveUser();
        user.IssuePasswordResetToken(Hash(token), DateTimeOffset.UtcNow.AddMinutes(-16), DateTimeOffset.UtcNow.AddMinutes(-1));
        var users = new InMemoryUserRepository();
        await users.AddAsync(user);
        var unitOfWork = new FakeUnitOfWork();
        var useCase = new ResetPasswordUseCase(
            users, new InMemoryRefreshTokenRepository(), new FakePasswordHasher(), unitOfWork);

        var error = await Assert.ThrowsAsync<AppException>(() =>
            useCase.ExecuteAsync(new ResetPasswordRequest(token, "NewPassword456!")));

        Assert.Equal("identity.invalid_password_reset_token", error.Code);
        Assert.Equal(0, unitOfWork.SaveChangesCalls);
    }

    [Fact]
    public async Task ResetPassword_ReplayedToken_IsRejected()
    {
        const string token = "replayed-reset-token";
        var user = CreateActiveUser();
        user.IssuePasswordResetToken(Hash(token), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMinutes(15));
        var users = new InMemoryUserRepository();
        await users.AddAsync(user);
        var useCase = new ResetPasswordUseCase(
            users, new InMemoryRefreshTokenRepository(), new FakePasswordHasher(), new FakeUnitOfWork());
        var request = new ResetPasswordRequest(token, "NewPassword456!");

        await useCase.ExecuteAsync(request);
        var error = await Assert.ThrowsAsync<AppException>(() => useCase.ExecuteAsync(request));

        Assert.Equal("identity.invalid_password_reset_token", error.Code);
    }

    private static User CreateActiveUser()
    {
        var user = User.Create(
            Guid.NewGuid(), "active@example.com", "hashed_OldPassword123!", "Active User",
            DateTimeOffset.UtcNow, "verification-hash", DateTimeOffset.UtcNow.AddHours(24));
        Assert.True(user.TryVerify("verification-hash", DateTimeOffset.UtcNow));
        return user;
    }

    private static RefreshToken CreateRefreshToken(Guid userId, string value) =>
        RefreshToken.Create(Guid.NewGuid(), userId, value, DateTimeOffset.UtcNow.AddDays(7), DateTimeOffset.UtcNow);

    private static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
