using System.Runtime.CompilerServices;
using System.Text.Json;
using Diet.Application.Abstractions;
using Diet.Application.Common;
using Diet.Domain.Entities;
using Longevia.Recommendation.Contracts.V1;
using Longevia.Shared.Auth;
using Longevia.Shared.Errors;
using Microsoft.Extensions.Options;

namespace Diet.Application.Features.Knowledge;

/// <summary>
/// Chat hỏi sách: lưu hội thoại (user data thuộc Diet DB) và stream câu trả lời
/// grounded từ Recommendation. Mỗi cuộc hội thoại gửi tối đa 6 lượt gần nhất để
/// model hiểu ngữ cảnh và chọn đại từ đúng.
/// </summary>
public sealed class KnowledgeConversationUseCases(
    IKnowledgeConversationRepository repository,
    IUnitOfWork uow,
    IKnowledgeProvider provider,
    ICurrentUser user,
    TimeProvider time,
    IOptions<RecommendationBudgetOptions> budget)
{
    private const int HistoryLimit = 6;
    private const string FallbackDisclaimer =
        "Thông tin chỉ mang tính tham khảo từ sách, không thay thế tư vấn y khoa.";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<KnowledgeConversationSummaryResponse>> ListAsync(CancellationToken ct)
    {
        var userId = user.RequiredId();
        var conversations = await repository.ListAsync(userId, 30, ct);
        return conversations
            .Select(x => new KnowledgeConversationSummaryResponse(
                x.Id, x.Title, x.Messages.Count, x.UpdatedAt))
            .ToArray();
    }

    public async Task<KnowledgeConversationDetailResponse> GetAsync(Guid id, CancellationToken ct)
    {
        var conversation = await repository.GetAsync(id, user.RequiredId(), ct)
            ?? throw AppException.NotFound("Không tìm thấy hội thoại.", "diet.conversation_not_found");

        return new KnowledgeConversationDetailResponse(
            conversation.Id,
            conversation.Title,
            conversation.Messages
                .OrderBy(x => x.CreatedAt)
                .Select(ToMessageResponse)
                .ToArray());
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var conversation = await repository.GetAsync(id, user.RequiredId(), ct)
            ?? throw AppException.NotFound("Không tìm thấy hội thoại.", "diet.conversation_not_found");

        repository.Remove(conversation);
        await uow.SaveChangesAsync(ct);
    }

    /// <summary>Stream một lượt hỏi đáp và lưu cả câu hỏi lẫn câu trả lời.</summary>
    public async IAsyncEnumerable<KnowledgeStreamChunk> AskStreamAsync(
        Guid? conversationId,
        string question,
        int? chapterFilter,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var userId = user.RequiredId();
        var now = time.GetUtcNow();

        var conversation = conversationId is null
            ? null
            : await repository.GetAsync(conversationId.Value, userId, ct);

        if (conversationId is not null && conversation is null)
        {
            yield return new KnowledgeStreamError("diet.conversation_not_found",
                "Hội thoại không tồn tại hoặc không thuộc tài khoản của bạn.");
            yield break;
        }

        conversation ??= KnowledgeConversation.Create(userId, question, now);
        if (conversationId is null)
        {
            repository.Add(conversation);
        }

        var history = conversation.Messages
            .Where(x => !string.IsNullOrWhiteSpace(x.Content))
            .OrderBy(x => x.CreatedAt)
            .TakeLast(HistoryLimit)
            .Select(x => new Longevia.Recommendation.Contracts.V1.ConversationTurn
            {
                Role = x.Role,
                Content = x.Content,
            })
            .ToList();

        var userMessage = KnowledgeMessage.Create(
            conversation.Id, "user", question, "[]", string.Empty, now);
        conversation.AddMessage(userMessage);
        repository.AddMessage(userMessage);
        await uow.SaveChangesAsync(ct);

        var request = new AskLongevityQuestionStreamRequest
        {
            UserId = userId.ToString("D"),
            Question = question,
            ChapterFilter = chapterFilter ?? 0,
        };
        request.History.AddRange(history);

        var answer = new System.Text.StringBuilder();
        var citations = new List<KnowledgeCitationResponse>();
        var abstained = false;
        var abstainReason = string.Empty;
        var disclaimer = string.Empty;
        var failed = false;

        var stream = provider.AskStreamAsync(request, Deadline(), ct).GetAsyncEnumerator(ct);
        await using (stream.ConfigureAwait(false))
        {
            while (true)
            {
                AskLongevityQuestionEvent? current = null;
                string? streamErrorCode = null;
                string? streamErrorMessage = null;
                var cancelled = false;

                try
                {
                    if (!await stream.MoveNextAsync().ConfigureAwait(false))
                    {
                        break;
                    }

                    current = stream.Current;
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    cancelled = true;
                }
                catch (AppException ex)
                {
                    failed = true;
                    streamErrorCode = ex.Code;
                    streamErrorMessage = ex.Message;
                }

                if (cancelled)
                {
                    yield break;
                }

                if (streamErrorCode is not null)
                {
                    yield return new KnowledgeStreamError(streamErrorCode, streamErrorMessage ?? string.Empty);
                    break;
                }

                switch (current!.EventCase)
                {
                    case AskLongevityQuestionEvent.EventOneofCase.Delta:
                        answer.Append(current.Delta);
                        yield return new KnowledgeStreamDelta(current.Delta);
                        break;
                    case AskLongevityQuestionEvent.EventOneofCase.Citation:
                        var citation = KnowledgeMapper.Citation(current.Citation);
                        citations.Add(citation);
                        yield return new KnowledgeStreamCitation(citation);
                        break;
                    case AskLongevityQuestionEvent.EventOneofCase.Done:
                        abstained = current.Done.Abstained;
                        abstainReason = current.Done.AbstainReason;
                        disclaimer = current.Done.Disclaimer?.Text ?? string.Empty;
                        break;
                    case AskLongevityQuestionEvent.EventOneofCase.Error:
                        failed = true;
                        yield return new KnowledgeStreamError(current.Error.Code, current.Error.Message);
                        break;
                }
            }
        }

        if (failed)
        {
            yield break;
        }

        var assistant = KnowledgeMessage.Create(
            conversation.Id,
            "assistant",
            answer.ToString(),
            JsonSerializer.Serialize(citations, JsonOptions),
            abstainReason,
            time.GetUtcNow());
        conversation.AddMessage(assistant);
        repository.AddMessage(assistant);
        await uow.SaveChangesAsync(ct);

        yield return new KnowledgeStreamDone(conversation.Id, assistant.Id, abstained, abstainReason,
            string.IsNullOrWhiteSpace(disclaimer) ? FallbackDisclaimer : disclaimer);
    }

    private DateTimeOffset Deadline() =>
        time.GetUtcNow().AddMilliseconds(budget.Value.DeadlineMilliseconds);

    private static KnowledgeMessageResponse ToMessageResponse(KnowledgeMessage message)
    {
        IReadOnlyList<KnowledgeCitationResponse> citations;
        try
        {
            citations = JsonSerializer.Deserialize<KnowledgeCitationResponse[]>(
                message.CitationsJson, JsonOptions) ?? [];
        }
        catch (JsonException)
        {
            citations = [];
        }

        return new KnowledgeMessageResponse(message.Id, message.Role, message.Content, citations,
            message.AbstainReason, message.CreatedAt);
    }
}
