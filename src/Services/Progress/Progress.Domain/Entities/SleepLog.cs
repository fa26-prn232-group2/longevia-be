using Progress.Domain.Constants;

namespace Progress.Domain.Entities;

public sealed class SleepLog
{
    private SleepLog() { }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public DateTimeOffset SleptAt { get; private set; }
    public DateTimeOffset WokeAt { get; private set; }
    public string SleepQuality { get; private set; } = null!;
    public double DurationHours { get; private set; }
    public DateOnly Date { get; private set; }

    public static SleepLog Create(Guid userId, DateTimeOffset sleptAt, DateTimeOffset wokeAt, string quality)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("UserId không được để trống.", nameof(userId));
        }

        if (wokeAt <= sleptAt)
        {
            throw new ArgumentException("Thời điểm thức dậy phải sau thời điểm đi ngủ.", nameof(wokeAt));
        }

        var duration = (wokeAt - sleptAt).TotalHours;
        if (duration > 24)
        {
            throw new ArgumentOutOfRangeException(nameof(wokeAt), "Thời gian ngủ không được vượt quá 24 giờ.");
        }

        if (!Constants.SleepQuality.All.Contains(quality))
        {
            throw new ArgumentException($"Chất lượng giấc ngủ '{quality}' không hợp lệ.", nameof(quality));
        }

        // Q01 / Q05: Gán ngày theo wake time (UTC DateOnly)
        var date = DateOnly.FromDateTime(wokeAt.UtcDateTime);

        return new SleepLog
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            SleptAt = sleptAt,
            WokeAt = wokeAt,
            SleepQuality = quality,
            DurationHours = Math.Round(duration, 2),
            Date = date
        };
    }
}
