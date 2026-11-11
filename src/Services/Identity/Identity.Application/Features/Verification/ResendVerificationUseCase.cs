using System.Security.Cryptography;
using Identity.Application.Abstractions;
using Identity.Domain.Repositories;
using Longevia.Shared.Events;

namespace Identity.Application.Features.Verification;

/// <summary>
/// Gửi lại email xác minh cho tài khoản còn chờ kích hoạt.
///
/// Luôn im lặng với email không tồn tại / đã kích hoạt / bị khóa: endpoint trả 202
/// như nhau để không biến chức năng này thành công cụ dò email tồn tại.
/// </summary>
public sealed class ResendVerificationUseCase(
    IUserRepository userRepository,
    IEventPublisher eventPublisher,
    IUnitOfWork unitOfWork)
{
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromHours(24);

    public async Task ExecuteAsync(ResendVerificationRequest request, CancellationToken ct = default)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var user = await userRepository.GetByEmailAsync(normalizedEmail, ct);
        if (user is null)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var tokenHash = VerificationTokenHasher.Hash(token);
        if (!user.TryReissueVerificationToken(tokenHash, now, now.Add(TokenLifetime)))
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
