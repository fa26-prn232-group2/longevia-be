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
