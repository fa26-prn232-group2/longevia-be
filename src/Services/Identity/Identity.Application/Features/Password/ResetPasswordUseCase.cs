using Identity.Application.Abstractions;
using Identity.Application.Common.Errors;
using Identity.Domain.Repositories;

namespace Identity.Application.Features.Password;

public sealed class ResetPasswordUseCase(
    IUserRepository userRepository,
    IRefreshTokenRepository refreshTokenRepository,
    IPasswordHasher passwordHasher,
    IUnitOfWork unitOfWork)
{
    public async Task ExecuteAsync(ResetPasswordRequest request, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var tokenHash = PasswordResetTokenHasher.Hash(request.Token);
        await using var transaction = await unitOfWork.BeginTransactionAsync(ct);
        var reset = await userRepository.TryResetPasswordAsync(
            tokenHash,
            passwordHasher.Hash(request.NewPassword),
            now,
            ct);

        if (!reset)
        {
            throw AuthErrors.InvalidPasswordResetToken();
        }

        var user = await userRepository.GetByPasswordResetTokenHashAsync(
            tokenHash, ct);
        if (user is null)
        {
            throw AuthErrors.InvalidPasswordResetToken();
        }

        await refreshTokenRepository.RevokeAllForUserAsync(user.Id, now, ct);
        await unitOfWork.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }
}
