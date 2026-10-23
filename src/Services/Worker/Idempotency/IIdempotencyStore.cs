namespace Longevia.Worker.Idempotency;

/// <summary>
/// Exactly-once effect guard for consumers: the first delivery claims the key,
/// redeliveries observe an existing claim and are acked without side effects.
/// Keys look like <c>consumed:{SchemaVersion}:{UserId|Email}:{OccurredOn.Ticks}</c>
/// (data-model §1.3) with a 7-day TTL.
/// </summary>
public interface IIdempotencyStore
{
    /// <summary>
    /// Atomically claims <paramref name="key"/> for <paramref name="ttl"/>.
    /// Returns true when this caller won the claim (process the event),
    /// false when the event was already processed (ack and skip).
    /// </summary>
    Task<bool> TryClaimAsync(string key, TimeSpan ttl, CancellationToken cancellationToken = default);
}
