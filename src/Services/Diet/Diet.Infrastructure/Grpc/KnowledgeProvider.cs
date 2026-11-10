using Diet.Application.Abstractions;
using Grpc.Core;
using Longevia.Recommendation.Contracts.V1;
using Longevia.Shared.Errors;

namespace Diet.Infrastructure.Grpc;

public sealed class KnowledgeProvider(RecommendationService.RecommendationServiceClient client,
    TimeProvider time) : IKnowledgeProvider
{
    public async Task<AskLongevityQuestionResponse> AskAsync(AskLongevityQuestionRequest request,
        DateTimeOffset deadline, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (deadline <= time.GetUtcNow()) throw Timeout();
        try
        {
            return await client.AskLongevityQuestionAsync(request, deadline: deadline.UtcDateTime,
                cancellationToken: ct).ResponseAsync;
        }
        catch (RpcException) when (ct.IsCancellationRequested) { throw new OperationCanceledException(ct); }
        catch (RpcException ex) { throw Map(ex); }
    }

    public async Task<SearchKnowledgeResponse> SearchAsync(SearchKnowledgeRequest request,
        DateTimeOffset deadline, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (deadline <= time.GetUtcNow()) throw Timeout();
        try
        {
            return await client.SearchLongevityKnowledgeAsync(request, deadline: deadline.UtcDateTime,
                cancellationToken: ct).ResponseAsync;
        }
        catch (RpcException) when (ct.IsCancellationRequested) { throw new OperationCanceledException(ct); }
        catch (RpcException ex) { throw Map(ex); }
    }

    public async IAsyncEnumerable<AskLongevityQuestionEvent> AskStreamAsync(
        AskLongevityQuestionStreamRequest request,
        DateTimeOffset deadline,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (deadline <= time.GetUtcNow()) throw Timeout();

        using var call = client.AskLongevityQuestionStream(request,
            deadline: deadline.UtcDateTime, cancellationToken: ct);

        while (true)
        {
            var (moved, current) = await MoveNextAsync(call.ResponseStream, ct);
            if (!moved)
            {
                yield break;
            }

            yield return current!;
        }
    }

    private async Task<(bool Moved, AskLongevityQuestionEvent? Current)> MoveNextAsync(
        IAsyncStreamReader<AskLongevityQuestionEvent> stream,
        CancellationToken ct)
    {
        try
        {
            var moved = await stream.MoveNext(ct);
            return (moved, moved ? stream.Current : null);
        }
        catch (RpcException) when (ct.IsCancellationRequested)
        {
            throw new OperationCanceledException(ct);
        }
        catch (RpcException ex)
        {
            throw Map(ex);
        }
    }

    private static AppException Map(RpcException ex) => ex.StatusCode switch
    {
        StatusCode.DeadlineExceeded => Timeout(),
        StatusCode.Unavailable => AppException.ServiceUnavailable(
            "Tra cứu tri thức tạm thời không khả dụng.", "diet.ai_unavailable"),
        StatusCode.InvalidArgument => AppException.BadRequest(
            "Không thể tra cứu với dữ liệu hiện tại.", "diet.invalid_knowledge_request"),
        _ => new AppException("Dịch vụ tri thức trả dữ liệu không hợp lệ.", 502,
            "Dịch vụ phụ thuộc trả dữ liệu không hợp lệ.", "diet.ai_integration_failed"),
    };

    private static AppException Timeout() =>
        new("Tra cứu tri thức quá thời gian chờ.", 504, "Dịch vụ phụ thuộc quá hạn.", "diet.ai_timeout");
}
