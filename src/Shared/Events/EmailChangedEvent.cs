using System.Text.Json.Serialization;

namespace Longevia.Shared.Events;

/// <summary>
/// Identity phát sau khi email đã đổi thành công; Worker gửi thông báo tới địa chỉ cũ.
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Skip)]
public sealed record EmailChangedEvent(
    Guid UserId,
    string OldEmail,
    string NewEmail,
    DateTimeOffset OccurredOn)
{
    public const int SchemaVersion = 1;
}
