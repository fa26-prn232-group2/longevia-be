using Diet.Domain.Entities;

namespace Diet.Application.Abstractions;

public interface IKnowledgeConversationRepository
{
    Task<KnowledgeConversation?> GetAsync(Guid id, Guid userId, CancellationToken ct);
    Task<IReadOnlyList<KnowledgeConversation>> ListAsync(Guid userId, int take, CancellationToken ct);
    void Add(KnowledgeConversation conversation);
    void AddMessage(KnowledgeMessage message);
    void Remove(KnowledgeConversation conversation);
}
