using Identity.Application.Abstractions;
using Identity.Application.Common.Dtos;
using Identity.Application.Common.Errors;
using Identity.Domain.Constants;
using Identity.Domain.Repositories;

namespace Identity.Application.Features.Auth;

public sealed class LoginUseCase(
    IUserRepository userRepository,
    IRefreshTokenRepository refreshTokenRepository,
    IPasswordHasher passwordHasher,
    IJwtTokenGenerator jwtTokenGenerator,
    IUnitOfWork unitOfWork)
{
    public async Task<AuthTokenResponse> ExecuteAsync(LoginRequest request, CancellationToken ct = default)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        var user = await userRepository.GetByEmailAsync(normalizedEmail, ct);
        if (user is null)
        {
            throw AuthErrors.InvalidCredentials();
        }

        var isPasswordValid = passwordHasher.Verify(request.Password, user.PasswordHash);
        if (!isPasswordValid)
        {
            throw AuthErrors.InvalidCredentials();
        }

        if (user.Status == UserStatus.Suspended)
        {
            throw UserErrors.Suspended();
        }

        if (user.Status != UserStatus.Active)
        {
            throw AuthErrors.InvalidCredentials();
        }

        var accessToken = jwtTokenGenerator.GenerateAccessToken(user);
        var refreshToken = jwtTokenGenerator.GenerateRefreshToken(user.Id);

        await refreshTokenRepository.AddAsync(refreshToken, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return new AuthTokenResponse(
            accessToken,
            refreshToken.Token,
            "Bearer",
            900);
    }
}
