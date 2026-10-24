using System.Security.Cryptography;
using Identity.Application.Abstractions;
using Identity.Domain.Constants;
using Identity.Domain.Repositories;
using Longevia.Shared.Events;

namespace Identity.Application.Features.Password;

public sealed class ForgotPasswordUseCase(
    IUserRepository userRepository,
    IEventPublisher eventPublisher,
    IUnitOfWork unitOfWork)
{
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromMinutes(15);

    public async Task ExecuteAsync(ForgotPasswordRequest request, CancellationToken ct = default)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var user = await userRepository.GetByEmailAsync(normalizedEmail, ct);
        if (user is null || user.Status != UserStatus.Active)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var expiresAt = now.Add(TokenLifetime);
        user.IssuePasswordResetToken(PasswordResetTokenHasher.Hash(token), now, expiresAt);
        userRepository.Update(user);

        await eventPublisher.PublishAsync(
            new PasswordResetRequestedEvent(user.Email, token, expiresAt, now),
            ct);
        await unitOfWork.SaveChangesAsync(ct);
    }
}
