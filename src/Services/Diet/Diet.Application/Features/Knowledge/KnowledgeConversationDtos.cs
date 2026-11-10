namespace Diet.Application.Features.Knowledge;

public sealed record KnowledgeConversationSummaryResponse(
    Guid Id,
    string Title,
    int MessageCount,
    DateTimeOffset UpdatedAt);

public sealed record KnowledgeMessageResponse(
    Guid Id,
    string Role,
    string Content,
    IReadOnlyList<KnowledgeCitationResponse> Citations,
    string AbstainReason,
    DateTimeOffset CreatedAt);

public sealed record KnowledgeConversationDetailResponse(
    Guid Id,
    string Title,
    IReadOnlyList<KnowledgeMessageResponse> Messages);

/// <summary>Sự kiện stream trả cho client qua SSE.</summary>
public abstract record KnowledgeStreamChunk;

public sealed record KnowledgeStreamDelta(string Text) : KnowledgeStreamChunk;

public sealed record KnowledgeStreamCitation(KnowledgeCitationResponse Citation) : KnowledgeStreamChunk;

public sealed record KnowledgeStreamDone(
    Guid ConversationId,
    Guid MessageId,
    bool Abstained,
    string AbstainReason,
    string Disclaimer) : KnowledgeStreamChunk;

public sealed record KnowledgeStreamError(string Code, string Message) : KnowledgeStreamChunk;
