namespace Longevia.Shared.Time;

/// <summary>
/// Ngày nghiệp vụ của Longevia (ADR006): mặc định Asia/Ho_Chi_Minh, cấu hình qua
/// <c>APP_TIMEZONE</c>. Mọi timestamp vẫn lưu UTC; chỉ việc quy đổi "ngày" cho
/// người dùng (water, dashboard, meal summary, digest, streak…) đi qua đây.
/// </summary>
public static class BusinessTime
{
    public const string DefaultTimeZoneId = "Asia/Ho_Chi_Minh";

    private static TimeZoneInfo _zone = CreateDefaultZone();

    /// <summary>IANA id đang dùng.</summary>
    public static string TimeZoneId => _zone.Id;

    /// <summary>Áp cấu hình timezone; id không hợp lệ thì giữ mặc định +7.</summary>
    public static void Configure(string? timeZoneId) => _zone = Resolve(timeZoneId);

    /// <summary>Resolve IANA id, fallback vùng +7 cố định.</summary>
    public static TimeZoneInfo Resolve(string? timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
        {
            return CreateDefaultZone();
        }

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId.Trim());
        }
        catch (TimeZoneNotFoundException)
        {
            return CreateDefaultZone();
        }
        catch (InvalidTimeZoneException)
        {
            return CreateDefaultZone();
        }
    }

    /// <summary>Ngày nghiệp vụ của một instant.</summary>
    public static DateOnly ToDate(DateTimeOffset instant) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, _zone).DateTime);

    /// <summary>Ngày nghiệp vụ hôm nay theo đồng hồ hệ thống.</summary>
    public static DateOnly Today() => ToDate(DateTimeOffset.UtcNow);

    /// <summary>Thứ Hai của tuần chứa <paramref name="date"/>.</summary>
    public static DateOnly WeekStart(DateOnly date) =>
        date.AddDays(-(((int)date.DayOfWeek + 6) % 7));

    /// <summary>Instant UTC của 00:00 ngày nghiệp vụ.</summary>
    public static DateTimeOffset StartOfDayUtc(DateOnly date)
    {
        var local = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        if (_zone.IsInvalidTime(local))
        {
            // Giờ mùa hè bị bỏ qua: dùng 01:00 để luôn có instant hợp lệ.
            local = local.AddHours(1);
        }

        var offset = _zone.GetUtcOffset(local);
        return new DateTimeOffset(local, offset).ToUniversalTime();
    }

    private static TimeZoneInfo CreateDefaultZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(DefaultTimeZoneId);
        }
        catch (Exception exception) when (exception is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            // Windows/container thiếu tzdata: dựng vùng +7 cố định.
            return TimeZoneInfo.CreateCustomTimeZone(
                "Asia/Ho_Chi_Minh+07",
                TimeSpan.FromHours(7),
                "Vietnam Time",
                "Vietnam Time");
        }
    }
}
