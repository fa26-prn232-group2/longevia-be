using System.Text.Json.Serialization;

namespace Longevia.Shared.Events;

/// <summary>
/// Published by Identity after a user verifies their email and the account becomes Active.
/// Consumer: Worker sends the welcome email exactly once.
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Skip)]
public sealed record UserVerifiedEvent(
    Guid UserId,
    string Email,
    string FullName,
    DateTimeOffset OccurredOn)
{
    public const int SchemaVersion = 1;
}
