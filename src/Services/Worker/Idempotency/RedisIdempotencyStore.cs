using StackExchange.Redis;

namespace Longevia.Worker.Idempotency;

/// <summary>
/// Redis-backed <see cref="IIdempotencyStore"/> using atomic SET NX with TTL —
/// safe across Worker replicas and restarts (Redis is ephemeral in dev, so a
/// cache wipe only risks a resend, never a loss).
/// </summary>
public sealed class RedisIdempotencyStore(IConnectionMultiplexer multiplexer) : IIdempotencyStore
{
    /// <inheritdoc />
    public async Task<bool> TryClaimAsync(string key, TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        var db = multiplexer.GetDatabase();
        return await db.StringSetAsync(key, "1", ttl, When.NotExists);
    }

    /// <inheritdoc />
    public async Task ReleaseAsync(string key, CancellationToken cancellationToken = default)
    {
        var db = multiplexer.GetDatabase();
        await db.KeyDeleteAsync(key);
    }
}
