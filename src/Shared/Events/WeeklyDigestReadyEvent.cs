using System.Text.Json.Serialization;

namespace Longevia.Shared.Events;

/// <summary>
/// Published by Progress on the weekly schedule after the user opted in.
/// Consumer: Worker renders and sends the weekly health digest.
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Skip)]
public sealed record WeeklyDigestReadyEvent(
    Guid EventId,
    Guid UserId,
    string Email,
    string FullName,
    string WeekStart,
    string WeekEnd,
    int AverageCalories,
    double AverageFastingHours,
    int CurrentStreak,
    int BestStreak,
    int DaysActive,
    DateTimeOffset OccurredOn)
{
    public const int SchemaVersion = 1;
}
