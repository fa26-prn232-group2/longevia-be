using Identity.Application.Abstractions;
using Identity.Application.Common.Dtos;
using Identity.Application.Common.Errors;
using Identity.Domain.Constants;
using Identity.Domain.Repositories;

namespace Identity.Application.Features.Auth;

public sealed class RefreshTokenUseCase(
    IUserRepository userRepository,
    IRefreshTokenRepository refreshTokenRepository,
    IJwtTokenGenerator jwtTokenGenerator,
    IUnitOfWork unitOfWork)
{
    public async Task<AuthTokenResponse> ExecuteAsync(RefreshTokenRequest request, CancellationToken ct = default)
    {
        var existingToken = await refreshTokenRepository.GetByTokenAsync(request.RefreshToken, ct);
        var now = DateTimeOffset.UtcNow;

        if (existingToken is null || !existingToken.IsActive(now))
        {
            throw AuthErrors.InvalidRefreshToken();
        }

        var user = await userRepository.GetByIdAsync(existingToken.UserId, ct);
        if (user is null || user.Status != UserStatus.Active)
        {
            throw AuthErrors.InvalidRefreshToken();
        }

        // Refresh Token Rotation
        var newRefreshToken = jwtTokenGenerator.GenerateRefreshToken(user.Id);
        existingToken.Revoke(now, newRefreshToken.Token);
        refreshTokenRepository.Update(existingToken);

        await refreshTokenRepository.AddAsync(newRefreshToken, ct);
        var newAccessToken = jwtTokenGenerator.GenerateAccessToken(user);

        await unitOfWork.SaveChangesAsync(ct);

        return new AuthTokenResponse(
            newAccessToken,
            newRefreshToken.Token,
            "Bearer",
            900);
    }
}
