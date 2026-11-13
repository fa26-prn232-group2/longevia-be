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
    IPendingRegistrationStore pendingStore,
    IUnitOfWork unitOfWork)
{
    public async Task<AuthTokenResponse> ExecuteAsync(LoginRequest request, CancellationToken ct = default)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        var user = await userRepository.GetByEmailAsync(normalizedEmail, ct);
        if (user is null)
        {
            await ThrowIfPendingRegistrationMatchesAsync(normalizedEmail, request.Password, ct);
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

        if (user.Status == UserStatus.PendingVerification)
        {
            // Chỉ lộ trạng thái sau khi mật khẩu đã đúng nên không tạo lỗ hổng dò email.
            throw AuthErrors.EmailNotVerified();
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

    private async Task ThrowIfPendingRegistrationMatchesAsync(
        string email,
        string password,
        CancellationToken ct)
    {
        var pending = await pendingStore.GetByEmailAsync(email, ct);
        if (pending is not null && passwordHasher.Verify(password, pending.PasswordHash))
        {
            // Chỉ lộ trạng thái sau khi mật khẩu đúng, giữ UX cũ và không tạo lỗ hổng dò email.
            throw AuthErrors.EmailNotVerified();
        }
    }
}
