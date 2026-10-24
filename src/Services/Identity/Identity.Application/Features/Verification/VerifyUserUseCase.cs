using Identity.Application.Abstractions;
using Identity.Application.Common.Errors;
using Identity.Domain.Repositories;

namespace Identity.Application.Features.Verification;

public sealed class VerifyUserUseCase(IUserRepository userRepository, IUnitOfWork unitOfWork)
{
    public async Task ExecuteAsync(VerifyUserRequest request, CancellationToken ct = default)
    {
        var verifiedAt = DateTimeOffset.UtcNow;
        var tokenHash = VerificationTokenHasher.Hash(request.Token);

        if (!await userRepository.TryVerifyTokenAsync(tokenHash, verifiedAt, ct))
        {
            throw UserErrors.InvalidVerificationToken();
        }

        await unitOfWork.SaveChangesAsync(ct);
    }
}
