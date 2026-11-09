using Longevia.Recommendation.Contracts.V1;

namespace Diet.Application.Abstractions;

public interface INutrientGapProvider
{
    Task<NutrientGapResponse> AnalyzeAsync(NutrientGapRequest request, DateTimeOffset deadline,
        CancellationToken ct);
}
