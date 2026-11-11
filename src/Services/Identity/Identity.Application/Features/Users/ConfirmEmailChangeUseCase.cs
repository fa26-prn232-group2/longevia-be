using Identity.Application.Abstractions;
using Identity.Application.Common.Errors;
using Identity.Domain.Repositories;
using Longevia.Shared.Events;

namespace Identity.Application.Features.Users;

/// <summary>
/// Xác nhận đổi email bằng token gửi tới địa chỉ mới. Thành công thì đổi email,
/// thu hồi toàn bộ refresh token (buộc đăng nhập lại) và thông báo tới email cũ.
/// </summary>
public sealed class ConfirmEmailChangeUseCase(
    IUserRepository userRepository,
    IRefreshTokenRepository refreshTokenRepository,
    IEventPublisher eventPublisher,
    IUnitOfWork unitOfWork)
{
    public async Task ExecuteAsync(ConfirmEmailChangeRequest request, CancellationToken ct = default)
    {
        var tokenHash = EmailChangeTokenHasher.Hash(request.Token);
        var user = await userRepository.GetByEmailChangeTokenHashAsync(tokenHash, ct);
        if (user is null || user.PendingEmail is null)
        {
            throw AuthErrors.InvalidEmailChangeToken();
        }

        var newEmail = user.PendingEmail;
        var existing = await userRepository.GetByEmailAsync(newEmail, ct);
        if (existing is not null && existing.Id != user.Id)
        {
            throw UserErrors.EmailTaken(newEmail);
        }

        var now = DateTimeOffset.UtcNow;
        var oldEmail = user.Email;
        if (!user.TryConsumeEmailChange(tokenHash, now))
        {
            throw AuthErrors.InvalidEmailChangeToken();
        }

        userRepository.Update(user);
        await refreshTokenRepository.RevokeAllForUserAsync(user.Id, now, ct);
        await eventPublisher.PublishAsync(
            new EmailChangedEvent(user.Id, oldEmail, newEmail, now), ct);
        await unitOfWork.SaveChangesAsync(ct);
    }
}
