namespace Longevia.Shared.Errors;

/// <summary>
/// Lỗi của một field cụ thể, dùng cho lỗi validation.
/// Theo <c>docs/system-design/design-pattern.md</c> §III.
/// </summary>
public sealed record FieldError(string Field, string Message);
