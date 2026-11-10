namespace Diet.Domain.Entities;

/// <summary>
/// Một cuộc hội thoại hỏi sách của người dùng. Lịch sử chat là dữ liệu người
/// dùng nên thuộc Diet DB (không nằm trong knowledge store của Recommendation).
/// </summary>
public sealed class KnowledgeConversation
{
    private readonly List<KnowledgeMessage> _messages = [];

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public IReadOnlyList<KnowledgeMessage> Messages => _messages;

    private KnowledgeConversation() { }

    public static KnowledgeConversation Create(Guid userId, string title, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        return new()
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Title = Truncate(title),
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    public void AddMessage(KnowledgeMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        _messages.Add(message);
        UpdatedAt = message.CreatedAt;
    }

    public void Touch(DateTimeOffset now) => UpdatedAt = now;

    private static string Truncate(string title)
    {
        const int max = 80;
        var trimmed = title.Trim();
        return trimmed.Length <= max ? trimmed : string.Concat(trimmed.AsSpan(0, max), "…");
    }
}
