using System.Security.Cryptography;
using Identity.Application.Abstractions;
using Identity.Application.Common.Errors;
using Identity.Application.Features.Verification;
using Identity.Domain.Repositories;
using Longevia.Shared.Events;

namespace Identity.Application.Features.Registration;

/// <summary>
/// Đăng ký tài khoản mới. Chưa ghi PostgreSQL: pending registration nằm trong
/// Redis với TTL 1 giờ và có thể bị ghi đè khi user đăng ký lại cùng email.
/// User DB chỉ được tạo ở bước verify email.
/// </summary>
public sealed class RegisterUserUseCase(
    IUserRepository userRepository,
    IPasswordHasher passwordHasher,
    IEventPublisher eventPublisher,
    IPendingRegistrationStore pendingStore,
    IUnitOfWork unitOfWork)
{
    public async Task<PendingRegistration> ExecuteAsync(RegisterUserRequest request, CancellationToken ct = default)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        if (await userRepository.ExistsByEmailAsync(normalizedEmail, ct))
        {
            throw UserErrors.EmailTaken(request.Email);
        }

        var now = DateTimeOffset.UtcNow;
        var verificationToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var pending = new PendingRegistration(
            Guid.NewGuid(),
            normalizedEmail,
            passwordHasher.Hash(request.Password),
            request.FullName.Trim(),
            VerificationTokenHasher.Hash(verificationToken),
            now,
            now.Add(PendingRegistration.Lifetime));

        await pendingStore.SaveAsync(pending, ct);

        await eventPublisher.PublishAsync(
            new UserRegisteredEvent(
                pending.Id,
                pending.Email,
                pending.FullName,
                verificationToken,
                now),
            ct);

        // Identity dùng MassTransit EF bus-outbox: cần SaveChanges để flush message
        // dù registration không ghi user vào PostgreSQL.
        await unitOfWork.SaveChangesAsync(ct);

        return pending;
    }
}
