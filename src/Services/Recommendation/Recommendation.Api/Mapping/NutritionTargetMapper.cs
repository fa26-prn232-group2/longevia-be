using Recommendation.Application.Features.Targets;
using Recommendation.Domain.Models;
using Contracts = Longevia.Recommendation.Contracts.V1;

namespace Recommendation.Api.Mapping;

/// <summary>Map gRPC ↔ Application cho gợi ý mục tiêu dinh dưỡng.</summary>
internal static class NutritionTargetMapper
{
    internal static SuggestNutritionTargetsRequest ToApplicationRequest(
        Contracts.SuggestNutritionTargetsRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var profile = request.Profile;

        return new SuggestNutritionTargetsRequest(
            new UserProfile
            {
                Age = profile?.Age ?? 0,
                Sex = profile?.Sex switch
                {
                    Contracts.BiologicalSex.Male => "MALE",
                    Contracts.BiologicalSex.Female => "FEMALE",
                    Contracts.BiologicalSex.Other => "OTHER",
                    _ => string.Empty,
                },
                HeightCm = profile?.HeightCm ?? 0,
                WeightKg = profile?.WeightKg ?? 0,
                ActivityLevel = profile?.ActivityLevel ?? string.Empty,
                DietaryPreference = profile?.DietaryPreference ?? string.Empty,
                Allergies = profile?.Allergies.ToList() ?? [],
            },
            request.Goal,
            request.Description,
            request.CurrentCalories,
            request.Locale);
    }

    internal static Contracts.SuggestNutritionTargetsResponse ToProtoResponse(
        SuggestNutritionTargetsResult result) =>
        new()
        {
            Calories = result.Calories,
            ProteinGrams = result.ProteinGrams,
            CarbsGrams = result.CarbsGrams,
            FatGrams = result.FatGrams,
            Rationale = result.Rationale,
            Source = result.Source,
            SafetyNote = result.SafetyNote,
        };
}
