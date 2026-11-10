namespace Diet.Domain.Entities;

/// <summary>
/// Một tin nhắn trong cuộc hội thoại hỏi sách. Citation được lưu dưới dạng JSON
/// snapshot để giữ nguyên đúng trích dẫn đã trả tại thời điểm đó.
/// </summary>
public sealed class KnowledgeMessage
{
    public Guid Id { get; private set; }
    public Guid ConversationId { get; private set; }
    /// <summary>"user" hoặc "assistant".</summary>
    public string Role { get; private set; } = string.Empty;
    public string Content { get; private set; } = string.Empty;
    public string CitationsJson { get; private set; } = "[]";
    public string AbstainReason { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }

    private KnowledgeMessage() { }

    public static KnowledgeMessage Create(Guid conversationId, string role, string content,
        string citationsJson, string abstainReason, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(role);
        return new()
        {
            Id = Guid.NewGuid(),
            ConversationId = conversationId,
            Role = role,
            Content = content,
            CitationsJson = string.IsNullOrWhiteSpace(citationsJson) ? "[]" : citationsJson,
            AbstainReason = abstainReason,
            CreatedAt = now,
        };
    }
}
