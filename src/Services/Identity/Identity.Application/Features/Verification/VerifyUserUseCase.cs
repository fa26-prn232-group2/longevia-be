using Identity.Application.Abstractions;
using Identity.Application.Common.Errors;
using Identity.Application.Features.Registration;
using Identity.Domain.Entities;
using Identity.Domain.Repositories;
using Longevia.Shared.Events;

namespace Identity.Application.Features.Verification;

/// <summary>
/// Xác thực email. Nguồn chính là pending registration trong Redis; user DB chỉ
/// được tạo ở đây. Giữ fallback cho token cũ còn nằm trong PostgreSQL để không
/// phá các tài khoản pending trước khi chuyển đổi.
/// </summary>
public sealed class VerifyUserUseCase(
    IUserRepository userRepository,
    IPendingRegistrationStore pendingStore,
    IUnitOfWork unitOfWork,
    IEventPublisher eventPublisher)
{
    public async Task ExecuteAsync(VerifyUserRequest request, CancellationToken ct = default)
    {
        var verifiedAt = DateTimeOffset.UtcNow;
        var tokenHash = VerificationTokenHasher.Hash(request.Token);

        var pending = await pendingStore.GetByTokenHashAsync(tokenHash, ct);
        if (pending is not null)
        {
            await VerifyPendingAsync(pending, verifiedAt, ct);
            return;
        }

        await VerifyLegacyDatabaseTokenAsync(tokenHash, verifiedAt, ct);
    }

    private async Task VerifyPendingAsync(
        PendingRegistration pending,
        DateTimeOffset verifiedAt,
        CancellationToken ct)
    {
        if (pending.ExpiresAt <= verifiedAt)
        {
            await pendingStore.DeleteAsync(pending, ct);
            throw UserErrors.InvalidVerificationToken();
        }

        // Email có thể đã được verify bởi request khác; token cũ không còn giá trị.
        if (await userRepository.ExistsByEmailAsync(pending.Email, ct))
        {
            throw UserErrors.InvalidVerificationToken();
        }

        var user = User.CreateActive(
            pending.Id,
            pending.Email,
            pending.PasswordHash,
            pending.FullName,
            pending.CreatedAt,
            verifiedAt);

        await userRepository.AddAsync(user, ct);
        await unitOfWork.SaveChangesAsync(ct);

        await eventPublisher.PublishAsync(
            new UserVerifiedEvent(user.Id, user.Email, user.FullName, verifiedAt),
            ct);
        await unitOfWork.SaveChangesAsync(ct);

        try
        {
            await pendingStore.DeleteAsync(pending, ct);
        }
        catch
        {
            // User đã được tạo; TTL sẽ dọn pending. Không làm verify fail sau khi DB commit.
        }
    }

    private async Task VerifyLegacyDatabaseTokenAsync(
        string tokenHash,
        DateTimeOffset verifiedAt,
        CancellationToken ct)
    {
        var user = await userRepository.GetByVerificationTokenHashAsync(tokenHash, ct);
        if (user is null
            || !await userRepository.TryVerifyTokenAsync(tokenHash, verifiedAt, ct))
        {
            throw UserErrors.InvalidVerificationToken();
        }

        await unitOfWork.SaveChangesAsync(ct);
        await eventPublisher.PublishAsync(
            new UserVerifiedEvent(user.Id, user.Email, user.FullName, verifiedAt),
            ct);
    }
}
