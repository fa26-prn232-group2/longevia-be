using Longevia.Worker.Idempotency;

namespace Worker.UnitTests.Fakes;

/// <summary>In-memory <see cref="IIdempotencyStore"/> — same claim semantics as Redis, no server.</summary>
public sealed class InMemoryIdempotencyStore : IIdempotencyStore
{
    private readonly HashSet<string> _claimed = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public Task<bool> TryClaimAsync(string key, TimeSpan ttl, CancellationToken cancellationToken = default) =>
        Task.FromResult(_claimed.Add(key));
}
