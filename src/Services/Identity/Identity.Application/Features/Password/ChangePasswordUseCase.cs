using Identity.Application.Abstractions;
using Identity.Application.Common.Errors;
using Identity.Domain.Constants;
using Identity.Domain.Repositories;
using Longevia.Shared.Auth;

namespace Identity.Application.Features.Password;

public sealed class ChangePasswordUseCase(
    ICurrentUser currentUser,
    IUserRepository userRepository,
    IRefreshTokenRepository refreshTokenRepository,
    IPasswordHasher passwordHasher,
    IUnitOfWork unitOfWork)
{
    public async Task ExecuteAsync(ChangePasswordRequest request, CancellationToken ct = default)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is null)
        {
            throw AuthErrors.Unauthorized();
        }

        var user = await userRepository.GetByIdAsync(currentUser.UserId.Value, ct);
        if (user is null || user.Status != UserStatus.Active)
        {
            throw AuthErrors.Unauthorized();
        }

        if (!passwordHasher.Verify(request.CurrentPassword, user.PasswordHash))
        {
            throw AuthErrors.InvalidCurrentPassword();
        }

        var changedAt = DateTimeOffset.UtcNow;
        await using var transaction = await unitOfWork.BeginTransactionAsync(ct);
        user.ChangePassword(passwordHasher.Hash(request.NewPassword), changedAt);
        userRepository.Update(user);
        await refreshTokenRepository.RevokeAllForUserAsync(user.Id, changedAt, ct);
        await unitOfWork.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }
}
