using Grpc.Core;
using Recommendation.Api.Mapping;
using Recommendation.Application.Features.Ask;
using Recommendation.Application.Features.LongevityHabits;
using Recommendation.Application.Features.MealPlans;
using Recommendation.Application.Features.Meals;
using Recommendation.Application.Features.NutrientGaps;
using Recommendation.Application.Features.Search;
using Recommendation.Application.Features.WorkoutPlans;
using Contracts = Longevia.Recommendation.Contracts.V1;

namespace Recommendation.Api.Endpoints;

/// <summary>
/// Endpoint gRPC của Recommendation. Mỗi phương thức chỉ map request → use case →
/// response; toàn bộ nghiệp vụ nằm ở tầng Application.
/// </summary>
internal sealed partial class RecommendationGrpcService(
    GetMealRecommendationsUseCase getMealRecommendations,
    AnalyzeNutrientGapsUseCase analyzeNutrientGaps,
    EvaluateLongevityHabitsUseCase evaluateLongevityHabits,
    AskLongevityQuestionUseCase askLongevityQuestion,
    SearchLongevityKnowledgeUseCase searchLongevityKnowledge,
    GenerateMealPlanUseCase generateMealPlan,
    GenerateWorkoutPlanUseCase generateWorkoutPlan)
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

    /// <summary>Phân tích thiếu hụt vi chất 7 ngày, deterministic theo bảng RDA (US5).</summary>
    public override async Task<Contracts.NutrientGapResponse> AnalyzeNutrientGaps(
        Contracts.NutrientGapRequest request,
        ServerCallContext context)
    {
        var applicationRequest = NutrientGapMapper.ToApplicationRequest(request);
        var result = await analyzeNutrientGaps
            .ExecuteAsync(applicationRequest, context.CancellationToken)
            .ConfigureAwait(false);

        return NutrientGapMapper.ToProtoResponse(result);
    }

    /// <summary>Đánh giá thói quen trường thọ theo rubric cố định, deterministic (US6).</summary>
    public override async Task<Contracts.LongevityHabitResponse> EvaluateLongevityHabits(
        Contracts.LongevityHabitRequest request,
        ServerCallContext context)
    {
        var applicationRequest = LongevityHabitMapper.ToApplicationRequest(request);
        var result = await evaluateLongevityHabits
            .ExecuteAsync(applicationRequest, context.CancellationToken)
            .ConfigureAwait(false);

        return LongevityHabitMapper.ToProtoResponse(result);
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

    /// <summary>Sinh kế hoạch ăn uống theo tuần từ candidate caller gửi (US1).</summary>
    public override async Task<Contracts.GenerateMealPlanResponse> GenerateMealPlan(
        Contracts.GenerateMealPlanRequest request,
        ServerCallContext context)
    {
        var applicationRequest = MealPlanMapper.ToApplicationRequest(request);
        var result = await generateMealPlan
            .ExecuteAsync(applicationRequest, context.CancellationToken)
            .ConfigureAwait(false);

        return MealPlanMapper.ToProtoResponse(result);
    }

    /// <summary>Sinh kế hoạch tập luyện theo tuần trong giới hạn caller gửi (US2).</summary>
    public override async Task<Contracts.GenerateWorkoutPlanResponse> GenerateWorkoutPlan(
        Contracts.GenerateWorkoutPlanRequest request,
        ServerCallContext context)
    {
        var applicationRequest = WorkoutPlanMapper.ToApplicationRequest(request);
        var result = await generateWorkoutPlan
            .ExecuteAsync(applicationRequest, context.CancellationToken)
            .ConfigureAwait(false);

        return WorkoutPlanMapper.ToProtoResponse(result);
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
