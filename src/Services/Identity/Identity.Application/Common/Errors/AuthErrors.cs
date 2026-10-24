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

    public static AppException InvalidPasswordResetToken() =>
        AppException.BadRequest(
            "Mã đặt lại mật khẩu không hợp lệ hoặc đã hết hạn.",
            "identity.invalid_password_reset_token");

    public static AppException InvalidCurrentPassword() =>
        AppException.BadRequest(
            "Mật khẩu hiện tại không chính xác.",
            "identity.invalid_current_password");
}
