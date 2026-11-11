using System.Text.Json.Serialization;

namespace Longevia.Shared.Events;

/// <summary>
/// Diet phát khi người dùng xoá một bữa ăn (toàn bộ món của một loại bữa trong ngày).
/// Progress dùng để rút lại đóng góp hoạt động/chuỗi ngày của bữa đó.
///
/// <c>EventId</c> là định danh của chính sự kiện (khác <c>MealLogId</c) vì một
/// MealLog chỉ có tối đa một sự kiện xoá.
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Skip)]
public sealed record MealLogRemovedEvent(
    Guid EventId,
    Guid UserId,
    Guid MealLogId,
    DateOnly BusinessDate,
    DateTimeOffset OccurredOn)
{
    public const int SchemaVersion = 1;
}
