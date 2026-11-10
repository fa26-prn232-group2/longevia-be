using Diet.Application.Abstractions;
using Grpc.Core;
using Longevia.Recommendation.Contracts.V1;
using Longevia.Shared.Errors;

namespace Diet.Infrastructure.Grpc;

public sealed class NutrientGapProvider(RecommendationService.RecommendationServiceClient client,
    TimeProvider time) : INutrientGapProvider
{
    public async Task<NutrientGapResponse> AnalyzeAsync(NutrientGapRequest request, DateTimeOffset deadline,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (deadline <= time.GetUtcNow()) throw Timeout();
        try
        {
            return await client.AnalyzeNutrientGapsAsync(request, deadline: deadline.UtcDateTime,
                cancellationToken: ct).ResponseAsync;
        }
        catch (RpcException) when (ct.IsCancellationRequested) { throw new OperationCanceledException(ct); }
        catch (RpcException ex)
        {
            throw ex.StatusCode switch
            {
                StatusCode.DeadlineExceeded => Timeout(),
                StatusCode.Unavailable => AppException.ServiceUnavailable(
                    "Phân tích vi chất tạm thời không khả dụng.", "diet.ai_unavailable"),
                StatusCode.InvalidArgument => AppException.BadRequest(
                    "Không thể phân tích với dữ liệu hiện tại.", "diet.invalid_nutrient_context"),
                _ => new AppException("Dịch vụ phân tích trả dữ liệu không hợp lệ.", 502,
                    "Dịch vụ phụ thuộc trả dữ liệu không hợp lệ.", "diet.ai_integration_failed"),
            };
        }
    }

    private static AppException Timeout() =>
        new("Phân tích vi chất quá thời gian chờ.", 504, "Dịch vụ phụ thuộc quá hạn.", "diet.ai_timeout");
}
