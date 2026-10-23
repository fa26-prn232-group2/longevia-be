namespace Identity.Domain.Entities;

public sealed class RefreshToken
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string Token { get; private set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public bool IsRevoked { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }
    public string? ReplacedByToken { get; private set; }

    public bool IsExpired(DateTimeOffset now) => now >= ExpiresAt;
    public bool IsActive(DateTimeOffset now) => !IsRevoked && !IsExpired(now);

    private RefreshToken()
    {
    }

    public static RefreshToken Create(
        Guid id,
        Guid userId,
        string token,
        DateTimeOffset expiresAt,
        DateTimeOffset createdAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);

        return new RefreshToken
        {
            Id = id,
            UserId = userId,
            Token = token,
            ExpiresAt = expiresAt,
            CreatedAt = createdAt,
            IsRevoked = false,
        };
    }

    public void Revoke(DateTimeOffset revokedAt, string? replacedByToken = null)
    {
        IsRevoked = true;
        RevokedAt = revokedAt;
        ReplacedByToken = replacedByToken;
    }
}
