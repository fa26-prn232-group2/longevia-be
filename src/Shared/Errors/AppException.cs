using System.Net;

namespace Longevia.Shared.Errors;

/// <summary>
/// Lỗi nghiệp vụ có kiểu: mang mã ổn định để client xử lý theo hợp đồng.
/// Theo <c>docs/system-design/design-pattern.md</c> §III.
/// </summary>
public sealed class AppException : Exception
{
    public AppException(
        string detail,
        int status,
        string title,
        string code,
        IReadOnlyList<FieldError>? errors = null)
        : base(detail)
    {
        Detail = detail;
        Status = status;
        Title = title;
        Code = code;
        Errors = errors;
    }

    /// <summary>
    /// Thông điệp người dùng thấy được (tiếng Việt). Không lộ chi tiết kỹ thuật
    /// hay lỗi DB.
    /// </summary>
    public string Detail { get; init; }

    /// <summary>
    /// Mã lỗi ổn định, ví dụ <c>recommendation.knowledge_unavailable</c>.
    /// Không đổi giá trị đã phát hành.
    /// </summary>
    public string Code { get; init; }

    /// <summary>HTTP status tương ứng; cũng là nguồn để map sang gRPC Status.</summary>
    public int Status { get; init; }

    /// <summary>Tiêu đề ngắn cho log và Problem Details.</summary>
    public string Title { get; init; }

    /// <summary>Lỗi theo field (validation); <c>null</c> nếu không phải lỗi field.</summary>
    public IReadOnlyList<FieldError>? Errors { get; init; }

    /// <summary>
    /// Trả về bản sao của lỗi với mã ổn định của feature. Mã gốc của factory
    /// chung là <c>system.*</c>.
    /// </summary>
    public AppException WithCode(string code) => new(Detail, Status, Title, code, Errors);

    /// <summary>Trả về bản sao của lỗi kèm danh sách lỗi theo field.</summary>
    public AppException WithErrors(IReadOnlyList<FieldError> errors) => new(Detail, Status, Title, Code, errors);

    public static AppException BadRequest(
        string detail,
        string code = "system.bad_request",
        IReadOnlyList<FieldError>? errors = null) =>
        new(detail, (int)HttpStatusCode.BadRequest, "Yêu cầu không hợp lệ.", code, errors);

    public static AppException Unauthorized(
        string detail,
        string code = "system.unauthorized") =>
        new(detail, (int)HttpStatusCode.Unauthorized, "Chưa xác thực.", code);

    public static AppException Forbidden(
        string detail,
        string code = "system.forbidden") =>
        new(detail, (int)HttpStatusCode.Forbidden, "Không có quyền truy cập.", code);

    public static AppException NotFound(
        string detail,
        string code = "system.not_found") =>
        new(detail, (int)HttpStatusCode.NotFound, "Không tìm thấy dữ liệu.", code);

    public static AppException Conflict(
        string detail,
        string code = "system.conflict") =>
        new(detail, (int)HttpStatusCode.Conflict, "Xung đột dữ liệu.", code);

    public static AppException Unprocessable(
        string detail,
        string code = "system.unprocessable",
        IReadOnlyList<FieldError>? errors = null) =>
        new(detail, (int)HttpStatusCode.UnprocessableEntity, "Không thể xử lý yêu cầu.", code, errors);

    public static AppException Internal(
        string detail = "Đã xảy ra lỗi hệ thống không mong muốn.",
        string code = "system.internal") =>
        new(detail, (int)HttpStatusCode.InternalServerError, "Lỗi hệ thống.", code);

    public static AppException ServiceUnavailable(
        string detail,
        string code = "system.service_unavailable") =>
        new(detail, (int)HttpStatusCode.ServiceUnavailable, "Dịch vụ tạm thời không khả dụng.", code);
}
