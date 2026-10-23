using Identity.Application.Abstractions;
using Identity.Domain.Repositories;

namespace Identity.Application.Features.Auth;

public sealed class LogoutUseCase(
    IRefreshTokenRepository refreshTokenRepository,
    IUnitOfWork unitOfWork)
{
    public async Task ExecuteAsync(LogoutRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return;
        }

        var token = await refreshTokenRepository.GetByTokenAsync(request.RefreshToken, ct);
        if (token is not null && !token.IsRevoked)
        {
            token.Revoke(DateTimeOffset.UtcNow);
            refreshTokenRepository.Update(token);
            await unitOfWork.SaveChangesAsync(ct);
        }
    }
}
