using Identity.Application.Abstractions;
using Identity.Domain.Entities;
using Identity.Domain.Repositories;
using Longevia.Shared.Auth;

namespace Identity.UnitTests.Common;

internal sealed class InMemoryUserRepository : IUserRepository
{
    private readonly List<User> _users = [];
    public IReadOnlyList<User> Users => _users;

    public Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(_users.FirstOrDefault(u => u.Id == id));

    public Task<User?> GetByIdWithProfileAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(_users.FirstOrDefault(u => u.Id == id));

    public Task<User?> GetByEmailAsync(string email, CancellationToken ct = default) =>
        Task.FromResult(_users.FirstOrDefault(u => u.Email.Equals(email, StringComparison.OrdinalIgnoreCase)));

    public Task<bool> ExistsByEmailAsync(string email, CancellationToken ct = default) =>
        Task.FromResult(_users.Any(u => u.Email.Equals(email, StringComparison.OrdinalIgnoreCase)));

    public Task<bool> TryVerifyTokenAsync(string tokenHash, DateTimeOffset verifiedAt, CancellationToken ct = default)
    {
        var user = _users.FirstOrDefault(candidate => candidate.TryVerify(tokenHash, verifiedAt));
        return Task.FromResult(user is not null);
    }

    public Task<User?> GetByPasswordResetTokenHashAsync(string tokenHash, CancellationToken ct = default) =>
        Task.FromResult(_users.FirstOrDefault(u => u.PasswordResetTokenHash == tokenHash));

    public Task<bool> TryResetPasswordAsync(
        string tokenHash,
        string passwordHash,
        DateTimeOffset resetAt,
        CancellationToken ct = default)
    {
        var user = _users.FirstOrDefault(candidate =>
            candidate.TryConsumePasswordResetToken(tokenHash, passwordHash, resetAt));
        return Task.FromResult(user is not null);
    }

    public Task AddAsync(User user, CancellationToken ct = default)
    {
        _users.Add(user);
        return Task.CompletedTask;
    }

    public void Update(User user)
    {
        var idx = _users.FindIndex(u => u.Id == user.Id);
        if (idx >= 0)
        {
            _users[idx] = user;
        }
    }

    public void AddProfile(UserProfile profile)
    {
    }
}

internal sealed class InMemoryRefreshTokenRepository : IRefreshTokenRepository
{
    private readonly List<RefreshToken> _tokens = [];
    public IReadOnlyList<RefreshToken> Tokens => _tokens;

    public Task<RefreshToken?> GetByTokenAsync(string token, CancellationToken ct = default) =>
        Task.FromResult(_tokens.FirstOrDefault(t => t.Token == token));

    public Task AddAsync(RefreshToken token, CancellationToken ct = default)
    {
        _tokens.Add(token);
        return Task.CompletedTask;
    }

    public Task RevokeAllForUserAsync(Guid userId, DateTimeOffset revokedAt, CancellationToken ct = default)
    {
        foreach (var token in _tokens.Where(token => token.UserId == userId && !token.IsRevoked))
        {
            token.Revoke(revokedAt);
        }

        return Task.CompletedTask;
    }

    public void Update(RefreshToken token)
    {
        var idx = _tokens.FindIndex(t => t.Id == token.Id);
        if (idx >= 0)
        {
            _tokens[idx] = token;
        }
    }
}

internal sealed class FakePasswordHasher : IPasswordHasher
{
    public string Hash(string password) => $"hashed_{password}";
    public bool Verify(string password, string passwordHash) => passwordHash == $"hashed_{password}";
}

internal sealed class FakeEventPublisher : IEventPublisher
{
    public List<object> PublishedEvents { get; } = [];

    public Task PublishAsync<T>(T @event, CancellationToken ct = default) where T : class
    {
        PublishedEvents.Add(@event);
        return Task.CompletedTask;
    }
}

internal sealed class FakeUnitOfWork : IUnitOfWork
{
    public int SaveChangesCalls { get; private set; }

    public Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        SaveChangesCalls++;
        return Task.FromResult(1);
    }

    public Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken ct = default) =>
        Task.FromResult<IUnitOfWorkTransaction>(new FakeUnitOfWorkTransaction());

    private sealed class FakeUnitOfWorkTransaction : IUnitOfWorkTransaction
    {
        public Task CommitAsync(CancellationToken ct = default) => Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}

internal sealed class FakeCurrentUser : ICurrentUser
{
    public Guid? UserId { get; set; }
    public string? Email { get; set; }
    public bool IsAuthenticated => UserId.HasValue;
    public bool InRole(string role) => false;
}

internal sealed class FakeJwtTokenGenerator : IJwtTokenGenerator
{
    public string GenerateAccessToken(User user, IReadOnlyList<string>? roles = null) =>
        $"fake_jwt_for_{user.Id}";

    public RefreshToken GenerateRefreshToken(Guid userId) =>
        RefreshToken.Create(Guid.NewGuid(), userId, "fake_refresh_token", DateTimeOffset.UtcNow.AddDays(7), DateTimeOffset.UtcNow);
}
