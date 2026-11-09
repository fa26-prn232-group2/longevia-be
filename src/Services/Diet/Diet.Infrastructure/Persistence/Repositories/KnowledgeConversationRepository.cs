using Diet.Application.Abstractions;
using Diet.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Diet.Infrastructure.Persistence.Repositories;

public sealed class KnowledgeConversationRepository(DietDbContext db) : IKnowledgeConversationRepository
{
    public async Task<KnowledgeConversation?> GetAsync(Guid id, Guid userId, CancellationToken ct) =>
        await db.KnowledgeConversations
            .Include(x => x.Messages)
            .FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, ct);

    public async Task<IReadOnlyList<KnowledgeConversation>> ListAsync(Guid userId, int take,
        CancellationToken ct) =>
        await db.KnowledgeConversations.AsNoTracking()
            .Include(x => x.Messages)
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.UpdatedAt)
            .Take(Math.Clamp(take, 1, 50))
            .ToListAsync(ct);

    public void Add(KnowledgeConversation conversation) => db.KnowledgeConversations.Add(conversation);

    public void AddMessage(KnowledgeMessage message) => db.KnowledgeMessages.Add(message);

    public void Remove(KnowledgeConversation conversation) => db.KnowledgeConversations.Remove(conversation);
}
