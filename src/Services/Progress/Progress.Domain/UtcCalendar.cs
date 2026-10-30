namespace Progress.Domain;

// Q01 MVP: calendar dates derived from instants are UTC; Diet DateOnly is never converted.
public static class UtcCalendar
{
    public static DateOnly Day(DateTimeOffset instant) => DateOnly.FromDateTime(instant.UtcDateTime);

    public static DateTimeOffset StartOfWeek(DateTimeOffset instant)
    {
        var day = Day(instant);
        var monday = day.AddDays(-((int)day.DayOfWeek + 6) % 7);
        return new DateTimeOffset(monday.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
    }
}
