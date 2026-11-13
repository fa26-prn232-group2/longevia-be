using Identity.Domain.Constants;
using Identity.Domain.Events;

namespace Identity.Domain.Entities;

public sealed class User
{
    private readonly List<RefreshToken> _refreshTokens = [];
    private readonly List<IDomainEvent> _domainEvents = [];

    public Guid Id { get; private set; }
    public string Email { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public string FullName { get; private set; } = string.Empty;
    public string Status { get; private set; } = UserStatus.PendingVerification;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }
    public string? VerificationTokenHash { get; private set; }
    public DateTimeOffset? VerificationTokenExpiresAt { get; private set; }
    public DateTimeOffset? VerificationTokenConsumedAt { get; private set; }
    public string? PasswordResetTokenHash { get; private set; }
    public DateTimeOffset? PasswordResetTokenExpiresAt { get; private set; }
    public DateTimeOffset? PasswordResetTokenConsumedAt { get; private set; }
    public string? PendingEmail { get; private set; }
    public string? EmailChangeTokenHash { get; private set; }
    public DateTimeOffset? EmailChangeTokenExpiresAt { get; private set; }
    public DateTimeOffset? EmailChangeTokenConsumedAt { get; private set; }
    public DateTimeOffset? EmailChangeRequestedAt { get; private set; }

    public UserProfile? Profile { get; private set; }
    public IReadOnlyCollection<RefreshToken> RefreshTokens => _refreshTokens.AsReadOnly();
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    private User()
    {
    }

    public static User Create(
        Guid id,
        string email,
        string passwordHash,
        string fullName,
        DateTimeOffset createdAt,
        string verificationTokenHash,
        DateTimeOffset verificationTokenExpiresAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(fullName);
        ArgumentException.ThrowIfNullOrWhiteSpace(verificationTokenHash);

        var user = new User
        {
            Id = id,
            Email = email.Trim().ToLowerInvariant(),
            PasswordHash = passwordHash,
            FullName = fullName.Trim(),
            Status = UserStatus.PendingVerification,
            CreatedAt = createdAt,
            VerificationTokenHash = verificationTokenHash,
            VerificationTokenExpiresAt = verificationTokenExpiresAt,
        };

        user._domainEvents.Add(new UserRegisteredDomainEvent(
            user.Id,
            user.Email,
            user.FullName,
            createdAt));

        return user;
    }

    /// <summary>
    /// Tạo user đã xác thực từ pending registration (Redis). Không phát lại
    /// <see cref="UserRegisteredDomainEvent"/> vì email xác thực đã được gửi ở bước đăng ký.
    /// </summary>
    public static User CreateActive(
        Guid id,
        string email,
        string passwordHash,
        string fullName,
        DateTimeOffset createdAt,
        DateTimeOffset activatedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(fullName);

        return new User
        {
            Id = id,
            Email = email.Trim().ToLowerInvariant(),
            PasswordHash = passwordHash,
            FullName = fullName.Trim(),
            Status = UserStatus.Active,
            CreatedAt = createdAt,
            UpdatedAt = activatedAt,
            VerificationTokenConsumedAt = activatedAt,
        };
    }

    public void SetProfile(UserProfile profile)
    {
        Profile = profile;
        UpdatedAt = profile.UpdatedAt;
        _domainEvents.Add(new UserProfileUpdatedDomainEvent(Id, profile.UpdatedAt));
    }

    public void AddRefreshToken(RefreshToken refreshToken)
    {
        _refreshTokens.Add(refreshToken);
    }

    public void ChangePassword(string newPasswordHash, DateTimeOffset updatedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(newPasswordHash);
        PasswordHash = newPasswordHash;
        UpdatedAt = updatedAt;
    }

    /// <summary>Đổi tên hiển thị; tên được trim và giới hạn 1–100 ký tự.</summary>
    public void ChangeFullName(string fullName, DateTimeOffset updatedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fullName);
        var trimmed = fullName.Trim();
        if (trimmed.Length is < 1 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(fullName), "Họ tên phải từ 1 đến 100 ký tự.");
        }

        FullName = trimmed;
        UpdatedAt = updatedAt;
    }

    public void IssuePasswordResetToken(string tokenHash, DateTimeOffset issuedAt, DateTimeOffset expiresAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenHash);
        PasswordResetTokenHash = tokenHash;
        UpdatedAt = issuedAt;
        PasswordResetTokenExpiresAt = expiresAt;
        PasswordResetTokenConsumedAt = null;
    }

    public bool TryConsumePasswordResetToken(string tokenHash, string passwordHash, DateTimeOffset resetAt)
    {
        if (Status != UserStatus.Active
            || PasswordResetTokenConsumedAt is not null
            || PasswordResetTokenExpiresAt is null
            || PasswordResetTokenExpiresAt <= resetAt
            || !string.Equals(PasswordResetTokenHash, tokenHash, StringComparison.Ordinal))
        {
            return false;
        }

        ChangePassword(passwordHash, resetAt);
        PasswordResetTokenConsumedAt = resetAt;
        return true;
    }

    /// <summary>
    /// Cấp lại token xác minh cho tài khoản còn chờ kích hoạt (dùng cho "Gửi lại email").
    /// Tài khoản đã kích hoạt hoặc bị khóa trả về false để không gửi email vô nghĩa.
    /// </summary>
    public bool TryReissueVerificationToken(string verificationTokenHash,
        DateTimeOffset issuedAt, DateTimeOffset expiresAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(verificationTokenHash);

        if (Status != UserStatus.PendingVerification)
        {
            return false;
        }

        VerificationTokenHash = verificationTokenHash;
        VerificationTokenExpiresAt = expiresAt;
        VerificationTokenConsumedAt = null;
        UpdatedAt = issuedAt;
        return true;
    }

    /// <summary>
    /// Ghi nhận yêu cầu đổi email: lưu email mới chờ xác nhận + token. Chỉ tài khoản
    /// đang Active mới được đổi; email mới chưa được áp dụng cho tới khi xác nhận.
    /// </summary>
    public void RequestEmailChange(string newEmail, string tokenHash, DateTimeOffset requestedAt,
        DateTimeOffset expiresAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(newEmail);
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenHash);
        if (Status != UserStatus.Active)
        {
            throw new InvalidOperationException("Tài khoản chưa kích hoạt không thể đổi email.");
        }

        var normalized = newEmail.Trim().ToLowerInvariant();
        if (normalized.Length > 320 || !normalized.Contains('@', StringComparison.Ordinal))
        {
            throw new ArgumentException("Email mới không hợp lệ.", nameof(newEmail));
        }

        PendingEmail = normalized;
        EmailChangeTokenHash = tokenHash;
        EmailChangeTokenExpiresAt = expiresAt;
        EmailChangeTokenConsumedAt = null;
        EmailChangeRequestedAt = requestedAt;
        UpdatedAt = requestedAt;
    }

    /// <summary>Xác nhận đổi email bằng token; token dùng một lần và có hạn.</summary>
    public bool TryConsumeEmailChange(string tokenHash, DateTimeOffset confirmedAt)
    {
        if (Status != UserStatus.Active
            || PendingEmail is null
            || EmailChangeTokenConsumedAt is not null
            || EmailChangeTokenExpiresAt is null
            || EmailChangeTokenExpiresAt <= confirmedAt
            || !string.Equals(EmailChangeTokenHash, tokenHash, StringComparison.Ordinal))
        {
            return false;
        }

        Email = PendingEmail;
        PendingEmail = null;
        EmailChangeTokenHash = null;
        EmailChangeTokenExpiresAt = null;
        EmailChangeTokenConsumedAt = confirmedAt;
        UpdatedAt = confirmedAt;
        return true;
    }

    public bool TryVerify(string verificationTokenHash, DateTimeOffset verifiedAt)
    {
        if (Status != UserStatus.PendingVerification
            || VerificationTokenConsumedAt is not null
            || VerificationTokenExpiresAt is null
            || VerificationTokenExpiresAt <= verifiedAt
            || !string.Equals(VerificationTokenHash, verificationTokenHash, StringComparison.Ordinal))
        {
            return false;
        }

        Status = UserStatus.Active;
        VerificationTokenConsumedAt = verifiedAt;
        UpdatedAt = verifiedAt;
        return true;
    }

    public void ClearDomainEvents()
    {
        _domainEvents.Clear();
    }
}
