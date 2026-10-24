using Progress.Domain.Constants;

namespace Progress.Domain.Entities;

public sealed class FastingSession
{
    private FastingSession() { }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string Protocol { get; private set; } = null!;
    public int TargetHours { get; private set; }
    public DateTimeOffset StartedAt { get; private set; }
    public DateTimeOffset? EndedAt { get; private set; }
    public FastingStatus Status { get; private set; }

    public DateTimeOffset TargetEndedAt => StartedAt.AddHours(TargetHours);

    public static FastingSession Start(Guid userId, string protocol, int targetHours, DateTimeOffset startedAt)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("UserId không được để trống.", nameof(userId));
        }

        if (!FastingProtocol.All.Contains(protocol))
        {
            throw new ArgumentException($"Giao thức nhịn ăn '{protocol}' không hợp lệ.", nameof(protocol));
        }

        if (FastingProtocol.TryGetPresetTargetHours(protocol, out var presetHours))
        {
            if (targetHours != presetHours)
            {
                throw new ArgumentException($"Giao thức {protocol} yêu cầu targetHours là {presetHours}.", nameof(targetHours));
            }
        }
        else if (protocol.Equals(FastingProtocol.Custom, StringComparison.OrdinalIgnoreCase))
        {
            if (targetHours < 1 || targetHours > 24)
            {
                throw new ArgumentOutOfRangeException(nameof(targetHours), "Giao thức Custom yêu cầu targetHours từ 1 đến 24 giờ.");
            }
        }

        return new FastingSession
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Protocol = protocol,
            TargetHours = targetHours,
            StartedAt = startedAt,
            Status = FastingStatus.InProgress
        };
    }

    public void End(DateTimeOffset endedAt)
    {
        if (Status == FastingStatus.Completed)
        {
            return;
        }

        if (endedAt < StartedAt)
        {
            throw new ArgumentException("Thời điểm kết thúc không được trước thời điểm bắt đầu.", nameof(endedAt));
        }

        EndedAt = endedAt;
        Status = FastingStatus.Completed;
    }

    public double ActualHours => EndedAt.HasValue
        ? Math.Round((EndedAt.Value - StartedAt).TotalHours, 2)
        : 0;

    public bool IsGoalReached => EndedAt.HasValue && (EndedAt.Value - StartedAt).TotalHours >= TargetHours;
}
