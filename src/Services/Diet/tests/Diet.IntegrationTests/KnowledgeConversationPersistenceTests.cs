using Diet.Domain.Entities;
using Diet.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Diet.IntegrationTests;

public sealed class KnowledgeConversationPersistenceTests(DietApiFactory factory)
    : IClassFixture<DietApiFactory>
{
    [Fact]
    public async Task Conversation_SavesUserThenAssistantMessageAcrossTwoSaves()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DietDbContext>();
        var userId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var conversation = KnowledgeConversation.Create(userId, "Chất xơ", now);
        db.KnowledgeConversations.Add(conversation);

        var userMessage = KnowledgeMessage.Create(conversation.Id, "user", "ăn gì để đủ chất xơ?",
            "[]", string.Empty, now);
        conversation.AddMessage(userMessage);
        db.KnowledgeMessages.Add(userMessage);
        await db.SaveChangesAsync();

        var assistantMessage = KnowledgeMessage.Create(conversation.Id, "assistant", "rau xanh",
            "[]", string.Empty, now.AddSeconds(1));
        conversation.AddMessage(assistantMessage);
        db.KnowledgeMessages.Add(assistantMessage);
        await db.SaveChangesAsync();

        await using var verifyScope = factory.Services.CreateAsyncScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<DietDbContext>();
        var saved = await verifyDb.KnowledgeConversations.AsNoTracking()
            .Include(x => x.Messages)
            .FirstAsync(x => x.Id == conversation.Id);

        Assert.Equal(2, saved.Messages.Count);
        Assert.Equal("assistant", saved.Messages.OrderBy(x => x.CreatedAt).Last().Role);
    }
}
