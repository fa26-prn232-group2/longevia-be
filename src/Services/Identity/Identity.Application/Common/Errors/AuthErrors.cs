using Longevia.Shared.Errors;

namespace Identity.Application.Common.Errors;

public static class AuthErrors
{
    public static AppException InvalidCredentials() =>
        AppException.Unauthorized(
            "Email hoặc mật khẩu không chính xác.",
            "identity.invalid_credentials");

    public static AppException InvalidRefreshToken() =>
        AppException.Unauthorized(
            "Refresh token không hợp lệ hoặc đã hết hạn.",
            "identity.invalid_refresh_token");

    public static AppException Unauthorized() =>
        AppException.Unauthorized(
            "Yêu cầu xác thực để thực hiện thao tác này.",
            "identity.unauthorized");
}
