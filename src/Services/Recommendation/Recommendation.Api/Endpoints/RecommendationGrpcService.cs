using Grpc.Core;
using Recommendation.Api.Mapping;
using Recommendation.Application.Features.Meals;
using Contracts = Longevia.Recommendation.Contracts.V1;

namespace Recommendation.Api.Endpoints;

/// <summary>
/// Endpoint gRPC của Recommendation. Mỗi phương thức chỉ map request → use case →
/// response; toàn bộ nghiệp vụ nằm ở tầng Application.
/// </summary>
internal sealed partial class RecommendationGrpcService(GetMealRecommendationsUseCase getMealRecommendations)
    : Contracts.RecommendationService.RecommendationServiceBase
{
    /// <summary>Gợi ý bữa ăn trong danh mục an toàn do caller gửi (US1).</summary>
    public override async Task<Contracts.MealRecommendationResponse> GetMealRecommendations(
        Contracts.MealRecommendationRequest request,
        ServerCallContext context)
    {
        var applicationRequest = MealRecommendationMapper.ToApplicationRequest(request);
        var result = await getMealRecommendations
            .ExecuteAsync(applicationRequest, context.CancellationToken)
            .ConfigureAwait(false);

        return MealRecommendationMapper.ToProtoResponse(result);
    }
}
