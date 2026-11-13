using Identity.Application.Features.Registration;

namespace Identity.Application.Abstractions;

/// <summary>
/// Lưu đăng ký chưa xác thực (Redis ở production, in-memory ở test).
/// Ghi đè theo email để user có thể đăng ký lại khi chưa nhận được email.
/// </summary>
public interface IPendingRegistrationStore
{
    Task SaveAsync(PendingRegistration registration, CancellationToken ct = default);

    Task<PendingRegistration?> GetByEmailAsync(string email, CancellationToken ct = default);

    Task<PendingRegistration?> GetByTokenHashAsync(string tokenHash, CancellationToken ct = default);

    Task DeleteAsync(PendingRegistration registration, CancellationToken ct = default);
}
