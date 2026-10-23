using Longevia.Shared.Errors;

namespace Identity.Application.Common.Errors;

public static class UserErrors
{
    public static AppException NotFound(Guid id) =>
        AppException.NotFound(
            $"Không tìm thấy người dùng với ID '{id}'.",
            "identity.user_not_found");

    public static AppException NotFoundByEmail(string email) =>
        AppException.NotFound(
            $"Không tìm thấy người dùng với email '{email}'.",
            "identity.user_not_found");

    public static AppException EmailTaken(string email) =>
        AppException.Conflict(
            $"Email '{email}' đã được sử dụng.",
            "identity.email_taken");

    public static AppException InvalidVerificationToken() =>
        AppException.BadRequest(
            "Token xác minh không hợp lệ, đã hết hạn hoặc đã được sử dụng.",
            "identity.invalid_verification_token");

    public static AppException Suspended() =>
        AppException.Forbidden(
            "Tài khoản của bạn đã bị khóa.",
            "identity.user_suspended");
}
