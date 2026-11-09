using Identity.Application.Abstractions;
using Identity.Application.Common.Errors;
using Identity.Domain.Repositories;
using Longevia.Shared.Events;

namespace Identity.Application.Features.Verification;

public sealed class VerifyUserUseCase(IUserRepository userRepository, IUnitOfWork unitOfWork,
    IEventPublisher eventPublisher)
{
    public async Task ExecuteAsync(VerifyUserRequest request, CancellationToken ct = default)
    {
        var verifiedAt = DateTimeOffset.UtcNow;
        var tokenHash = VerificationTokenHasher.Hash(request.Token);
        var user = await userRepository.GetByVerificationTokenHashAsync(tokenHash, ct);

        if (!await userRepository.TryVerifyTokenAsync(tokenHash, verifiedAt, ct) || user is null)
        {
            throw UserErrors.InvalidVerificationToken();
        }

        await unitOfWork.SaveChangesAsync(ct);
        await eventPublisher.PublishAsync(new UserVerifiedEvent(user.Id, user.Email, user.FullName,
            verifiedAt), ct);
    }
}
