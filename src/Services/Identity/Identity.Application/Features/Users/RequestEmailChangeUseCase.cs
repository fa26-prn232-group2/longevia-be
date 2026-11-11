using System.Security.Cryptography;
using Identity.Application.Abstractions;
using Identity.Application.Common.Errors;
using Identity.Domain.Constants;
using Identity.Domain.Repositories;
using Longevia.Shared.Auth;
using Longevia.Shared.Events;

namespace Identity.Application.Features.Users;

/// <summary>
/// Yêu cầu đổi email: xác minh mật khẩu hiện tại, lưu email mới chờ xác nhận và phát
/// sự kiện để Worker gửi link xác nhận tới email mới + cảnh báo tới email cũ.
/// </summary>
public sealed class RequestEmailChangeUseCase(
    ICurrentUser currentUser,
    IUserRepository userRepository,
    IPasswordHasher passwordHasher,
    IEventPublisher eventPublisher,
    IUnitOfWork unitOfWork)
{
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromHours(24);
    private static readonly TimeSpan Cooldown = TimeSpan.FromMinutes(5);

    public async Task ExecuteAsync(RequestEmailChangeRequest request, CancellationToken ct = default)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is null)
        {
            throw AuthErrors.Unauthorized();
        }

        var user = await userRepository.GetByIdWithProfileAsync(currentUser.UserId.Value, ct);
        if (user is null)
        {
            throw UserErrors.NotFound(currentUser.UserId.Value);
        }

        if (!passwordHasher.Verify(request.CurrentPassword, user.PasswordHash))
        {
            throw AuthErrors.InvalidCurrentPassword();
        }

        var newEmail = request.NewEmail.Trim().ToLowerInvariant();
        if (string.Equals(newEmail, user.Email, StringComparison.OrdinalIgnoreCase))
        {
            throw AuthErrors.EmailUnchanged();
        }

        var existing = await userRepository.GetByEmailAsync(newEmail, ct);
        if (existing is not null)
        {
            throw UserErrors.EmailTaken(newEmail);
        }

        var now = DateTimeOffset.UtcNow;
        if (user.PendingEmail is not null && user.EmailChangeRequestedAt is { } requestedAt
            && now - requestedAt < Cooldown)
        {
            throw AuthErrors.EmailChangeTooFrequent();
        }

        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var expiresAt = now.Add(TokenLifetime);
        user.RequestEmailChange(newEmail, EmailChangeTokenHasher.Hash(token), now, expiresAt);
        userRepository.Update(user);

        await eventPublisher.PublishAsync(
            new EmailChangeRequestedEvent(user.Id, user.Email, newEmail, user.FullName, token,
                expiresAt, now),
            ct);
        await unitOfWork.SaveChangesAsync(ct);
    }
}
