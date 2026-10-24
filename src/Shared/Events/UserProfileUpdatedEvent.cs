using System.Text.Json.Serialization;

namespace Longevia.Shared.Events;

/// <summary>
/// Signals that a user's profile changed without including health profile data.
/// Published by Identity through its transactional outbox.
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Skip)]
public sealed record UserProfileUpdatedEvent(Guid UserId, DateTimeOffset OccurredOn)
{
    /// <summary>Contract schema version; breaking changes require a new version.</summary>
    public const int SchemaVersion = 1;
}
