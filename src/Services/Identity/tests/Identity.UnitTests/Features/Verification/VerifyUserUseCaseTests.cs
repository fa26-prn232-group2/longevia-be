using System.Security.Cryptography;
using System.Text;
using Identity.Application.Features.Verification;
using Identity.Domain.Entities;
using Identity.UnitTests.Common;
using Longevia.Shared.Errors;

namespace Identity.UnitTests.Features.Verification;

public sealed class VerifyUserUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_ShouldVerifyUsingHashAndPersistOnce()
    {
        const string rawToken = "ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789";
        var tokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));
        var repository = new InMemoryUserRepository();
        var user = User.Create(Guid.NewGuid(), "verify@example.com", "password-hash", "Verify User", DateTimeOffset.UtcNow, tokenHash, DateTimeOffset.UtcNow.AddHours(24));
        await repository.AddAsync(user);
        var unitOfWork = new FakeUnitOfWork();
        var useCase = new VerifyUserUseCase(repository, unitOfWork);

        await useCase.ExecuteAsync(new VerifyUserRequest(rawToken));

        Assert.Equal("Active", user.Status);
        Assert.NotNull(user.VerificationTokenConsumedAt);
        Assert.Equal(1, unitOfWork.SaveChangesCalls);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldRejectInvalidTokenWithoutSaving()
    {
        var unitOfWork = new FakeUnitOfWork();
        var useCase = new VerifyUserUseCase(new InMemoryUserRepository(), unitOfWork);

        var exception = await Assert.ThrowsAsync<AppException>(() =>
            useCase.ExecuteAsync(new VerifyUserRequest(new string('0', 64))));

        Assert.Equal(400, exception.Status);
        Assert.Equal("identity.invalid_verification_token", exception.Code);
        Assert.Equal(0, unitOfWork.SaveChangesCalls);
    }
}
