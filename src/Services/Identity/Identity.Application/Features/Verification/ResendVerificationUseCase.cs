using System.Security.Cryptography;
using Identity.Application.Abstractions;
using Identity.Application.Features.Registration;
using Identity.Domain.Constants;
using Identity.Domain.Repositories;
using Longevia.Shared.Events;

namespace Identity.Application.Features.Verification;

/// <summary>
/// Gửi lại email xác thực. Pending registration trong Redis bị ghi đè token mới;
/// fallback PostgreSQL cho user pending cũ trong giai đoạn chuyển đổi.
/// Endpoint luôn 202 để không tiết lộ email có tồn tại hay không.
/// </summary>
public sealed class ResendVerificationUseCase(
    IUserRepository userRepository,
    IPendingRegistrationStore pendingStore,
    IEventPublisher eventPublisher,
    IUnitOfWork unitOfWork)
{
    public async Task ExecuteAsync(ResendVerificationRequest request, CancellationToken ct = default)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var now = DateTimeOffset.UtcNow;
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var tokenHash = VerificationTokenHasher.Hash(token);

        var pending = await pendingStore.GetByEmailAsync(normalizedEmail, ct);
        if (pending is not null)
        {
            var refreshed = pending with
            {
                VerificationTokenHash = tokenHash,
                ExpiresAt = now.Add(PendingRegistration.Lifetime),
            };

            await pendingStore.SaveAsync(refreshed, ct);
            await eventPublisher.PublishAsync(
                new UserRegisteredEvent(refreshed.Id, refreshed.Email, refreshed.FullName, token, now),
                ct);
            return;
        }

        await ResendForLegacyDatabaseUserAsync(normalizedEmail, token, tokenHash, now, ct);
    }

    private async Task ResendForLegacyDatabaseUserAsync(
        string normalizedEmail,
        string token,
        string tokenHash,
        DateTimeOffset now,
        CancellationToken ct)
    {
        var user = await userRepository.GetByEmailAsync(normalizedEmail, ct);
        if (user is null
            || user.Status != UserStatus.PendingVerification
            || !user.TryReissueVerificationToken(tokenHash, now, now.Add(PendingRegistration.Lifetime)))
        {
            return;
        }

        userRepository.Update(user);
        await eventPublisher.PublishAsync(
            new UserRegisteredEvent(user.Id, user.Email, user.FullName, token, now),
            ct);
        await unitOfWork.SaveChangesAsync(ct);
    }
}
