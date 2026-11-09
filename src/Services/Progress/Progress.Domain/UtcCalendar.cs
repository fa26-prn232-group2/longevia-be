namespace Progress.Domain;

/// <summary>
/// Ngày nghiệp vụ theo ADR006: mặc định Asia/Ho_Chi_Minh (UTC+7), cấu hình qua
/// <c>APP_TIMEZONE</c> ở tầng host. Timestamp vẫn lưu UTC; class này chỉ quy đổi
/// instant sang ngày/đầu tuần theo múi giờ nghiệp vụ.
/// </summary>
public static class UtcCalendar
{
    private static TimeZoneInfo _zone = CreateDefaultZone();

    public static TimeZoneInfo Zone => _zone;

    public static void Configure(TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(zone);
        _zone = zone;
    }

    public static DateOnly Day(DateTimeOffset instant) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, _zone).DateTime);

    public static DateTimeOffset StartOfWeek(DateTimeOffset instant)
    {
        var day = Day(instant);
        var monday = day.AddDays(-(((int)day.DayOfWeek + 6) % 7));
        return StartOfDayUtc(monday);
    }

    public static DateTimeOffset StartOfDayUtc(DateOnly date)
    {
        var local = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        if (_zone.IsInvalidTime(local))
        {
            local = local.AddHours(1);
        }

        return new DateTimeOffset(local, _zone.GetUtcOffset(local)).ToUniversalTime();
    }

    private static TimeZoneInfo CreateDefaultZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
        }
        catch (Exception exception) when (exception is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return TimeZoneInfo.CreateCustomTimeZone(
                "Asia/Ho_Chi_Minh+07", TimeSpan.FromHours(7), "Vietnam Time", "Vietnam Time");
        }
    }
}
