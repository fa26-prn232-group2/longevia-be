namespace Longevia.Worker.Idempotency;

/// <summary>
/// Idempotency guard for consumers: the first delivery claims the key,
/// successful processing keeps the claim, and failed processing releases it so
/// broker retries can try again. Redeliveries observing an existing claim are
/// acked without side effects.
/// Keys are derived from the schema version and the event's stable identity
/// (for example, user ID or a token hash) and use a 7-day TTL.
/// </summary>
public interface IIdempotencyStore
{
    /// <summary>
    /// Atomically claims <paramref name="key"/> for <paramref name="ttl"/>.
    /// Returns true when this caller won the claim (process the event),
    /// false when another delivery already holds the claim (ack and skip).
    /// </summary>
    Task<bool> TryClaimAsync(string key, TimeSpan ttl, CancellationToken cancellationToken = default);

    /// <summary>Releases a claim after processing failed so a broker retry can run.</summary>
    Task ReleaseAsync(string key, CancellationToken cancellationToken = default);
}
