namespace Identity.Application.Features.Registration;

/// <summary>
/// Đăng ký chưa xác thực được giữ trong Redis (TTL ngắn). Chỉ khi verify
/// thành công mới tạo bản ghi <c>User</c> trong PostgreSQL.
/// </summary>
public sealed record PendingRegistration(
    Guid Id,
    string Email,
    string PasswordHash,
    string FullName,
    string VerificationTokenHash,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt)
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(1);
}
