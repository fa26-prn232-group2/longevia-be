using Grpc.Core;
using Recommendation.Api.Mapping;
using Recommendation.Application.Features.Ask;
using Recommendation.Application.Features.Meals;
using Recommendation.Application.Features.Search;
using Contracts = Longevia.Recommendation.Contracts.V1;

namespace Recommendation.Api.Endpoints;

/// <summary>
/// Endpoint gRPC của Recommendation. Mỗi phương thức chỉ map request → use case →
/// response; toàn bộ nghiệp vụ nằm ở tầng Application.
/// </summary>
internal sealed partial class RecommendationGrpcService(
    GetMealRecommendationsUseCase getMealRecommendations,
    AskLongevityQuestionUseCase askLongevityQuestion,
    SearchLongevityKnowledgeUseCase searchLongevityKnowledge)
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

    /// <summary>Hỏi tri thức trong sách, có trích dẫn và abstain khi thiếu căn cứ (US3).</summary>
    public override async Task<Contracts.AskLongevityQuestionResponse> AskLongevityQuestion(
        Contracts.AskLongevityQuestionRequest request,
        ServerCallContext context)
    {
        var applicationRequest = AskLongevityQuestionMapper.ToApplicationRequest(request);
        var result = await askLongevityQuestion
            .ExecuteAsync(applicationRequest, context.CancellationToken)
            .ConfigureAwait(false);

        return AskLongevityQuestionMapper.ToProtoResponse(result);
    }

    /// <summary>Tra cứu các đoạn sách theo truy vấn tự do, có lọc theo chương (US4).</summary>
    public override async Task<Contracts.SearchKnowledgeResponse> SearchLongevityKnowledge(
        Contracts.SearchKnowledgeRequest request,
        ServerCallContext context)
    {
        var applicationRequest = SearchKnowledgeMapper.ToApplicationRequest(request);
        var result = await searchLongevityKnowledge
            .ExecuteAsync(applicationRequest, context.CancellationToken)
            .ConfigureAwait(false);

        return SearchKnowledgeMapper.ToProtoResponse(result);
    }
}
