using System.Text.Json.Serialization;

namespace Longevia.Shared.Events;

/// <summary>
/// Published by Identity when a user requests a password reset.
/// Consumer: Worker <c>worker.email</c> queue renders the reset email.
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Skip)]
public sealed record PasswordResetRequestedEvent(
    string Email,
    string ResetToken,
    DateTimeOffset ExpiresAt,
    DateTimeOffset OccurredOn)
{
    /// <summary>
    /// Contract schema version. Additive optional fields stay on version 1;
    /// renames, removals, or semantic changes require a new version.
    /// </summary>
    public const int SchemaVersion = 1;
}
