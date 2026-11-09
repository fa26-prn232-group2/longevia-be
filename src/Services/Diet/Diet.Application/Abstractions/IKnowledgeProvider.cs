using Longevia.Recommendation.Contracts.V1;

namespace Diet.Application.Abstractions;

public interface IKnowledgeProvider
{
    Task<AskLongevityQuestionResponse> AskAsync(AskLongevityQuestionRequest request, DateTimeOffset deadline,
        CancellationToken ct);
    IAsyncEnumerable<AskLongevityQuestionEvent> AskStreamAsync(AskLongevityQuestionStreamRequest request,
        DateTimeOffset deadline, CancellationToken ct);
    Task<SearchKnowledgeResponse> SearchAsync(SearchKnowledgeRequest request, DateTimeOffset deadline,
        CancellationToken ct);
}
