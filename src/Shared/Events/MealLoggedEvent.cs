using System.Text.Json.Serialization;

namespace Longevia.Shared.Events;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Skip)]
public sealed record MealLoggedEvent(
    Guid EventId,
    Guid UserId,
    Guid MealLogId,
    DateOnly BusinessDate,
    DateTimeOffset CreatedAt,
    DateTimeOffset OccurredOn)
{
    public const int SchemaVersion = 1;
}
