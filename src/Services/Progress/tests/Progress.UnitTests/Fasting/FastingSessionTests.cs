using Progress.Domain.Constants;
using Progress.Domain.Entities;

namespace Progress.UnitTests.Fasting;

public sealed class FastingSessionTests
{
    [Theory]
    [InlineData("16:8", 16)]
    [InlineData("18:6", 18)]
    [InlineData("20:4", 20)]
    public void Start_ValidPresetProtocols_SetsTargetHoursAndStatus(string protocol, int targetHours)
    {
        var userId = Guid.NewGuid();
        var startedAt = DateTimeOffset.UtcNow;

        var session = FastingSession.Start(userId, protocol, targetHours, startedAt);

        Assert.Equal(userId, session.UserId);
        Assert.Equal(protocol, session.Protocol);
        Assert.Equal(targetHours, session.TargetHours);
        Assert.Equal(startedAt, session.StartedAt);
        Assert.Equal(startedAt.AddHours(targetHours), session.TargetEndedAt);
        Assert.Equal(FastingStatus.InProgress, session.Status);
        Assert.Null(session.EndedAt);
        Assert.False(session.IsGoalReached);
    }

    [Theory]
    [InlineData("16:8", 15)]
    [InlineData("18:6", 20)]
    [InlineData("20:4", 16)]
    public void Start_PresetProtocolMismatch_ThrowsArgumentException(string protocol, int invalidHours)
    {
        var userId = Guid.NewGuid();
        var startedAt = DateTimeOffset.UtcNow;

        Assert.Throws<ArgumentException>(() => FastingSession.Start(userId, protocol, invalidHours, startedAt));
    }

    [Fact]
    public void Start_CustomProtocol_ValidTargetBetween1And24_Succeeds()
    {
        var userId = Guid.NewGuid();
        var startedAt = DateTimeOffset.UtcNow;

        var session = FastingSession.Start(userId, "Custom", 12, startedAt);

        Assert.Equal(12, session.TargetHours);
        Assert.Equal(FastingStatus.InProgress, session.Status);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(25)]
    [InlineData(-5)]
    public void Start_CustomProtocol_TargetOutOfRange_ThrowsArgumentOutOfRangeException(int targetHours)
    {
        var userId = Guid.NewGuid();
        var startedAt = DateTimeOffset.UtcNow;

        Assert.Throws<ArgumentOutOfRangeException>(() => FastingSession.Start(userId, "Custom", targetHours, startedAt));
    }

    [Fact]
    public void Start_InvalidProtocol_ThrowsArgumentException()
    {
        var userId = Guid.NewGuid();
        var startedAt = DateTimeOffset.UtcNow;

        Assert.Throws<ArgumentException>(() => FastingSession.Start(userId, "InvalidProtocol", 16, startedAt));
    }

    [Fact]
    public void End_ValidEndTime_SetsCompletedAndCalculatesActualHours()
    {
        var userId = Guid.NewGuid();
        var startedAt = DateTimeOffset.UtcNow;
        var session = FastingSession.Start(userId, "16:8", 16, startedAt);

        // 975 phút = 16.25 giờ
        var endedAt = startedAt.AddMinutes(975);
        session.End(endedAt);

        Assert.Equal(FastingStatus.Completed, session.Status);
        Assert.Equal(endedAt, session.EndedAt);
        Assert.Equal(16.25, session.ActualHours);
        Assert.True(session.IsGoalReached);
    }

    [Fact]
    public void End_UnderTarget_IsGoalReachedReturnsFalse()
    {
        var userId = Guid.NewGuid();
        var startedAt = DateTimeOffset.UtcNow;
        var session = FastingSession.Start(userId, "16:8", 16, startedAt);

        var endedAt = startedAt.AddHours(14);
        session.End(endedAt);

        Assert.Equal(FastingStatus.Completed, session.Status);
        Assert.Equal(14.0, session.ActualHours);
        Assert.False(session.IsGoalReached);
    }

    [Fact]
    public void End_EndedBeforeStarted_ThrowsArgumentException()
    {
        var userId = Guid.NewGuid();
        var startedAt = DateTimeOffset.UtcNow;
        var session = FastingSession.Start(userId, "16:8", 16, startedAt);

        Assert.Throws<ArgumentException>(() => session.End(startedAt.AddMinutes(-10)));
    }

    [Fact]
    public void End_AlreadyCompleted_IsIdempotent()
    {
        var userId = Guid.NewGuid();
        var startedAt = DateTimeOffset.UtcNow;
        var session = FastingSession.Start(userId, "16:8", 16, startedAt);

        var firstEnd = startedAt.AddHours(16);
        session.End(firstEnd);

        var secondEnd = startedAt.AddHours(18);
        session.End(secondEnd); // Should not change original endedAt

        Assert.Equal(firstEnd, session.EndedAt);
        Assert.Equal(16.0, session.ActualHours);
    }
}
