using Longevia.Diet.Contracts.V1;
using Longevia.Recommendation.Contracts.V1;
using Progress.Application.Abstractions;

namespace Progress.Infrastructure.Adapters.Grpc;

public sealed class LongevityProvider(RecommendationService.RecommendationServiceClient client) : ILongevityProvider
{
    public async Task<LongevityHabitResponse> EvaluateAsync(LongevityHabitRequest request,
        DateTimeOffset deadline, CancellationToken ct) =>
        await client.EvaluateLongevityHabitsAsync(request, deadline: deadline.UtcDateTime,
            cancellationToken: ct).ResponseAsync;
}

public sealed class NutritionAdherenceProvider(DietService.DietServiceClient client) : INutritionAdherenceProvider
{
    public async Task<NutritionAdherenceResponse> GetAsync(Guid userId, DateOnly from, DateOnly to,
        CancellationToken ct) =>
        await client.GetNutritionAdherenceAsync(new NutritionAdherenceRequest
        {
            UserId = userId.ToString("D"),
            FromDate = from.ToString("yyyy-MM-dd"),
            ToDate = to.ToString("yyyy-MM-dd"),
        }, deadline: DateTime.UtcNow.AddSeconds(5), cancellationToken: ct).ResponseAsync;
}
