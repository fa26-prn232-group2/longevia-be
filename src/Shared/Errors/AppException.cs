namespace Longevia.Shared.Errors;

public class AppException : Exception
{
    public string Code { get; init; }
    public int Status { get; init; }
    public string Title { get; init; }
    public IReadOnlyList<FieldError>? Errors { get; init; }

    public AppException(
        string detail,
        string code = "system.internal",
        int status = 500,
        string title = "Lỗi hệ thống",
        IReadOnlyList<FieldError>? errors = null)
        : base(detail)
    {
        Code = code;
        Status = status;
        Title = title;
        Errors = errors;
    }

    public AppException WithCode(string code) =>
        new(Message, code, Status, Title, Errors);

    public AppException WithErrors(IReadOnlyList<FieldError> errors) =>
        new(Message, Code, Status, Title, errors);

    public static AppException BadRequest(
        string detail,
        string code = "validation.failed",
        IReadOnlyList<FieldError>? errors = null) =>
        new(detail, code, 400, "Yêu cầu không hợp lệ", errors);

    public static AppException Unauthorized(
        string detail,
        string code = "auth.unauthorized") =>
        new(detail, code, 401, "Không có quyền truy cập");

    public static AppException Forbidden(
        string detail,
        string code = "auth.forbidden") =>
        new(detail, code, 403, "Bị từ chối truy cập");

    public static AppException NotFound(
        string detail,
        string code = "resource.not_found") =>
        new(detail, code, 404, "Không tìm thấy dữ liệu");

    public static AppException Conflict(
        string detail,
        string code = "resource.conflict") =>
        new(detail, code, 409, "Xung đột dữ liệu");

    public static AppException Unprocessable(
        string detail,
        string code = "validation.unprocessable",
        IReadOnlyList<FieldError>? errors = null) =>
        new(detail, code, 422, "Dữ liệu không thể xử lý", errors);

    public static AppException Internal(
        string detail = "Đã xảy ra lỗi hệ thống không mong muốn.",
        string code = "system.internal") =>
        new(detail, code, 500, "Lỗi hệ thống");

    public static AppException ServiceUnavailable(
        string detail,
        string code = "system.unavailable") =>
        new(detail, code, 503, "Dịch vụ tạm thời không khả dụng");
}
