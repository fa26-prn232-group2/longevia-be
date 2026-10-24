namespace Progress.Domain.Entities;

public sealed class ActivityContribution
{
    private ActivityContribution() { }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string Source { get; private set; } = null!;
    public string SourceLogId { get; private set; } = null!;
    public DateOnly Date { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public static ActivityContribution Create(
        Guid userId, string source, string sourceLogId, DateOnly date, DateTimeOffset createdAt)
    {
        return new ActivityContribution
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Source = source,
            SourceLogId = sourceLogId,
            Date = date,
            CreatedAt = createdAt
        };
    }
}
