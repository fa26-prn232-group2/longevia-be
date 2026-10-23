using System.Text.Json.Serialization;

namespace Longevia.Shared.Events;

/// <summary>
/// Published by Identity when a user account is registered successfully.
/// Consumer: Worker <c>worker.email</c> queue renders the verification email.
/// The producer publishes via transactional outbox (owned by the Identity feature).
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Skip)]
public sealed record UserRegisteredEvent(
    Guid UserId,
    string Email,
    string FullName,
    string VerificationToken,
    DateTimeOffset OccurredOn)
{
    /// <summary>
    /// Contract schema version. Additive optional fields stay on version 1;
    /// renames, removals, or semantic changes require a new version.
    /// </summary>
    public const int SchemaVersion = 1;
}
