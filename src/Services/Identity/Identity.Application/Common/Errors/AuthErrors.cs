using Longevia.Shared.Errors;

namespace Identity.Application.Common.Errors;

public static class AuthErrors
{
    public static AppException InvalidCredentials() =>
        AppException.Unauthorized(
            "Email hoặc mật khẩu không chính xác.",
            "identity.invalid_credentials");

    /// <summary>
    /// Mật khẩu đúng nhưng tài khoản chưa xác minh email. Dùng 403 (không phải 401)
    /// để client không kích hoạt luồng refresh token.
    /// </summary>
    public static AppException EmailNotVerified() =>
        AppException.Forbidden(
            "Tài khoản chưa xác minh email. Hãy mở email kích hoạt hoặc gửi lại email xác minh.",
            "identity.email_not_verified");

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

    public static AppException EmailUnchanged() =>
        AppException.BadRequest(
            "Email mới trùng với email hiện tại.",
            "identity.email_unchanged");

    public static AppException InvalidEmailChangeToken() =>
        AppException.BadRequest(
            "Link xác nhận đổi email không hợp lệ hoặc đã hết hạn.",
            "identity.invalid_email_change_token");

    public static AppException EmailChangeTooFrequent() =>
        new("Bạn vừa yêu cầu đổi email. Vui lòng chờ ít phút rồi thử lại.",
            429, "Quá nhiều yêu cầu", "identity.email_change_too_frequent");
}
