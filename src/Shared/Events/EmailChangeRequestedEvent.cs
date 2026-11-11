using System.Text.Json.Serialization;

namespace Longevia.Shared.Events;

/// <summary>
/// Identity phát khi người dùng yêu cầu đổi email. Worker gửi link xác nhận tới
/// địa chỉ mới và email cảnh báo tới địa chỉ cũ.
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Skip)]
public sealed record EmailChangeRequestedEvent(
    Guid UserId,
    string CurrentEmail,
    string NewEmail,
    string FullName,
    string Token,
    DateTimeOffset ExpiresAt,
    DateTimeOffset OccurredOn)
{
    public const int SchemaVersion = 1;
}
