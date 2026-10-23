using Identity.Application.Abstractions;
using Identity.Application.Common.Errors;
using Identity.Domain.Repositories;
using Longevia.Shared.Auth;

namespace Identity.Application.Features.Auth;

public sealed class LogoutUseCase(
    ICurrentUser currentUser,
    IRefreshTokenRepository refreshTokenRepository,
    IUnitOfWork unitOfWork)
{
    public async Task ExecuteAsync(LogoutRequest request, CancellationToken ct = default)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is null)
        {
            throw AuthErrors.Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return;
        }

        var token = await refreshTokenRepository.GetByTokenAsync(request.RefreshToken, ct);
        if (token is null || token.UserId != currentUser.UserId.Value)
        {
            throw AuthErrors.InvalidRefreshToken();
        }

        if (!token.IsRevoked)
        {
            token.Revoke(DateTimeOffset.UtcNow);
            refreshTokenRepository.Update(token);
            await unitOfWork.SaveChangesAsync(ct);
        }
    }
}
