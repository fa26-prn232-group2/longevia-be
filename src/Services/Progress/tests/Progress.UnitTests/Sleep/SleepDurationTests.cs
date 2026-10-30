using Progress.Domain.Constants;
using Progress.Domain.Entities;

namespace Progress.UnitTests.Sleep;

public sealed class SleepDurationTests
{
    [Fact]
    public void Create_ValidSleep_8Hours_CalculatesDurationAndWakeDate()
    {
        var userId = Guid.NewGuid();
        var sleptAt = DateTimeOffset.Parse("2026-10-24T23:00:00Z");
        var wokeAt = DateTimeOffset.Parse("2026-10-25T07:00:00Z");

        var sleep = SleepLog.Create(userId, sleptAt, wokeAt, SleepQuality.Good);

        Assert.Equal(8.0, sleep.DurationHours);
        Assert.Equal(new DateOnly(2026, 10, 25), sleep.Date);
        Assert.Equal(SleepQuality.Good, sleep.SleepQuality);
    }

    [Fact]
    public void Create_WokeBeforeSlept_ThrowsArgumentException()
    {
        var userId = Guid.NewGuid();
        var sleptAt = DateTimeOffset.Parse("2026-10-25T07:00:00Z");
        var wokeAt = DateTimeOffset.Parse("2026-10-24T23:00:00Z");

        Assert.Throws<ArgumentException>(() => SleepLog.Create(userId, sleptAt, wokeAt, SleepQuality.Good));
    }

    [Fact]
    public void Create_WokeEqualsSlept_ThrowsArgumentException()
    {
        var userId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        Assert.Throws<ArgumentException>(() => SleepLog.Create(userId, now, now, SleepQuality.Good));
    }

    [Fact]
    public void Create_DurationExceeds24Hours_ThrowsArgumentOutOfRangeException()
    {
        var userId = Guid.NewGuid();
        var sleptAt = DateTimeOffset.Parse("2026-10-23T20:00:00Z");
        var wokeAt = DateTimeOffset.Parse("2026-10-24T21:00:00Z"); // 25 hours

        Assert.Throws<ArgumentOutOfRangeException>(() => SleepLog.Create(userId, sleptAt, wokeAt, SleepQuality.Fair));
    }

    [Fact]
    public void Create_InvalidQuality_ThrowsArgumentException()
    {
        var userId = Guid.NewGuid();
        var sleptAt = DateTimeOffset.Parse("2026-10-24T23:00:00Z");
        var wokeAt = DateTimeOffset.Parse("2026-10-25T07:00:00Z");

        Assert.Throws<ArgumentException>(() => SleepLog.Create(userId, sleptAt, wokeAt, "SuperGood"));
    }
}
