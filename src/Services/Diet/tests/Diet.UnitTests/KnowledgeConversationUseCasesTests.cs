using Diet.Application.Abstractions;
using Diet.Application.Common;
using Diet.Application.Features.Knowledge;
using Diet.Domain.Entities;
using Longevia.Recommendation.Contracts.V1;
using Longevia.Shared.Auth;
using Longevia.Shared.Errors;
using Microsoft.Extensions.Options;

namespace Diet.UnitTests;

public sealed class KnowledgeConversationUseCasesTests
{
    private static readonly Guid UserId = Guid.NewGuid();

    [Fact]
    public async Task AskStream_CreatesConversationAndPersistsBothMessages()
    {
        var repo = new FakeRepository();
        var useCases = Create(repo, new FakeProvider());

        var chunks = await Collect(useCases.AskStreamAsync(null, "Nhịn ăn 16:8 có lợi gì?", null,
            CancellationToken.None));

        var delta = Assert.Single(chunks.OfType<KnowledgeStreamDelta>());
        Assert.Equal("Ổn định insulin.", delta.Text);
        Assert.Single(chunks.OfType<KnowledgeStreamCitation>());

        var done = Assert.Single(chunks.OfType<KnowledgeStreamDone>());
        Assert.False(done.Abstained);
        Assert.Equal(1, repo.Count);
        Assert.Equal(2, repo.Conversations[0].Messages.Count);
        Assert.Equal("user", repo.Conversations[0].Messages[0].Role);
        Assert.Equal("assistant", repo.Conversations[0].Messages[1].Role);
        Assert.Contains("insulin", repo.Conversations[0].Messages[1].Content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(done.ConversationId, repo.Conversations.Select(x => x.Id));
    }

    [Fact]
    public async Task AskStream_UnknownConversation_EmitsErrorWithoutPersisting()
    {
        var repo = new FakeRepository();
        var useCases = Create(repo, new FakeProvider());

        var chunks = await Collect(useCases.AskStreamAsync(Guid.NewGuid(), "hỏi gì đó", null,
            CancellationToken.None));

        var error = Assert.Single(chunks.OfType<KnowledgeStreamError>());
        Assert.Equal("diet.conversation_not_found", error.Code);
        Assert.Equal(0, repo.Count);
    }

    [Fact]
    public async Task List_ReturnsSummariesWithMessageCount()
    {
        var repo = new FakeRepository();
        var useCases = Create(repo, new FakeProvider());
        await Collect(useCases.AskStreamAsync(null, "câu hỏi", null, CancellationToken.None));

        var summaries = await useCases.ListAsync(CancellationToken.None);

        var summary = Assert.Single(summaries);
        Assert.Equal(2, summary.MessageCount);
        Assert.False(string.IsNullOrWhiteSpace(summary.Title));
    }

    [Fact]
    public async Task Delete_RemovesOwnedConversation()
    {
        var repo = new FakeRepository();
        var useCases = Create(repo, new FakeProvider());
        var done = (await Collect(useCases.AskStreamAsync(null, "câu hỏi", null,
            CancellationToken.None))).OfType<KnowledgeStreamDone>().Single();

        await useCases.DeleteAsync(done.ConversationId, CancellationToken.None);

        Assert.Equal(0, repo.Count);
    }

    [Fact]
    public async Task Get_ForeignConversation_ThrowsNotFound()
    {
        var repo = new FakeRepository();
        var useCases = Create(repo, new FakeProvider());

        await Assert.ThrowsAsync<AppException>(() =>
            useCases.GetAsync(Guid.NewGuid(), CancellationToken.None));
    }

    private static KnowledgeConversationUseCases Create(IKnowledgeConversationRepository repo,
        IKnowledgeProvider provider) =>
        new(repo, new FakeUnitOfWork(), provider, new FakeCurrentUser(), TimeProvider.System,
            Options.Create(new RecommendationBudgetOptions { DeadlineMilliseconds = 20_000 }));

    private static async Task<List<KnowledgeStreamChunk>> Collect(
        IAsyncEnumerable<KnowledgeStreamChunk> stream)
    {
        var items = new List<KnowledgeStreamChunk>();
        await foreach (var item in stream)
        {
            items.Add(item);
        }

        return items;
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken ct) => Task.FromResult(1);

        public async Task ExecuteInTransactionAsync(string lockKey,
            Func<CancellationToken, Task> action, CancellationToken ct) => await action(ct);
    }

    private sealed class FakeCurrentUser : ICurrentUser
    {
        public Guid? UserId => KnowledgeConversationUseCasesTests.UserId;
        public string? Email => "test@example.com";
        public bool IsAuthenticated => true;
        public bool InRole(string role) => false;
    }

    private sealed class FakeRepository : IKnowledgeConversationRepository
    {
        public List<KnowledgeConversation> Conversations { get; } = [];
        public int Count => Conversations.Count;

        public Task<KnowledgeConversation?> GetAsync(Guid id, Guid userId, CancellationToken ct) =>
            Task.FromResult(Conversations.FirstOrDefault(x => x.Id == id && x.UserId == userId));

        public Task<IReadOnlyList<KnowledgeConversation>> ListAsync(Guid userId, int take,
            CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<KnowledgeConversation>>(
                Conversations.Where(x => x.UserId == userId).Take(take).ToList());

        public void Add(KnowledgeConversation conversation) => Conversations.Add(conversation);

        public void AddMessage(KnowledgeMessage message)
        {
            var owner = Conversations.FirstOrDefault(x => x.Id == message.ConversationId);
            if (owner is not null && !owner.Messages.Contains(message))
            {
                owner.AddMessage(message);
            }
        }

        public void Remove(KnowledgeConversation conversation) => Conversations.Remove(conversation);
    }

    private sealed class FakeProvider : IKnowledgeProvider
    {
        public Task<AskLongevityQuestionResponse> AskAsync(AskLongevityQuestionRequest request,
            DateTimeOffset deadline, CancellationToken ct) =>
            throw new NotSupportedException();

        public async IAsyncEnumerable<AskLongevityQuestionEvent> AskStreamAsync(
            AskLongevityQuestionStreamRequest request, DateTimeOffset deadline,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
        {
            await Task.Yield();
            yield return new AskLongevityQuestionEvent { Delta = "Ổn định insulin." };
            yield return new AskLongevityQuestionEvent
            {
                Citation = new Citation
                {
                    ChapterNumber = 3,
                    ChapterTitle = "Nhịn ăn",
                    BookPage = 40,
                    PdfPage = 35,
                    Quote = "Nhịn ăn giúp cải thiện độ nhạy insulin.",
                    Verified = true,
                    SourceFile = "sach.md",
                },
            };
            yield return new AskLongevityQuestionEvent
            {
                Done = new AskLongevityQuestionDone
                {
                    Disclaimer = new Disclaimer { Text = "Chỉ mang tính tham khảo." },
                },
            };
        }

        public Task<SearchKnowledgeResponse> SearchAsync(SearchKnowledgeRequest request,
            DateTimeOffset deadline, CancellationToken ct) =>
            throw new NotSupportedException();
    }
}
