using Recommendation.Application.Features.Meals;
using Recommendation.Domain.Constants;
using Recommendation.Domain.Models;
using Contracts = Longevia.Recommendation.Contracts.V1;

namespace Recommendation.Api.Mapping;

/// <summary>
/// Map giữa message gRPC của contract và DTO nội bộ của Application. Contract
/// type chỉ tồn tại ở tầng Api nên Application không tham chiếu generated code.
/// </summary>
internal static class MealRecommendationMapper
{
    /// <summary>Chuyển request gRPC sang DTO nội bộ; hồ sơ thiếu thì dùng mặc định rỗng.</summary>
    internal static MealRecommendationRequest ToApplicationRequest(Contracts.MealRecommendationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new MealRecommendationRequest
        {
            MealSlot = ToMealSlot(request.MealType),
            Goal = request.Goal.ToString(),
            Profile = ToProfile(request.Profile),
            ConsumedCalories = request.ConsumedCalories,
            TargetCalories = request.TargetCalories,
            TargetProteinRemaining = request.TargetProteinRemaining,
            TargetCarbsRemaining = request.TargetCarbsRemaining,
            TargetFatRemaining = request.TargetFatRemaining,
            CandidateFoods = request.CandidateFoods.Select(ToCandidateFood).ToList(),
            Locale = string.IsNullOrWhiteSpace(request.Locale) ? "vi-VN" : request.Locale,
        };
    }

    /// <summary>Chuyển kết quả use case sang message phản hồi gRPC.</summary>
    internal static Contracts.MealRecommendationResponse ToProtoResponse(MealRecommendationResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        var response = new Contracts.MealRecommendationResponse
        {
            LongevityTip = result.LongevityTip,
            Disclaimer = new Contracts.Disclaimer { Text = result.Disclaimer },
            CalorieCapReached = result.CalorieCapReached,
            SafetyNote = result.SafetyNote,
        };

        response.Meals.AddRange(result.Meals.Select(ToProtoMeal));
        response.TipCitations.AddRange(result.TipCitations.Select(ToProtoCitation));

        return response;
    }

    private static MealSlot ToMealSlot(Contracts.MealType mealType) => mealType switch
    {
        Contracts.MealType.Breakfast => MealSlot.Breakfast,
        Contracts.MealType.Lunch => MealSlot.Lunch,
        Contracts.MealType.Dinner => MealSlot.Dinner,
        Contracts.MealType.Snack => MealSlot.Snack,
        _ => MealSlot.Unspecified,
    };

    private static UserProfile ToProfile(Contracts.UserProfile? profile)
    {
        if (profile is null)
        {
            return new UserProfile();
        }

        return new UserProfile
        {
            Age = profile.Age,
            Sex = profile.Sex.ToString(),
            HeightCm = profile.HeightCm,
            WeightKg = profile.WeightKg,
            ActivityLevel = profile.ActivityLevel,
            DietaryPreference = profile.DietaryPreference,
            Allergies = profile.Allergies.ToList(),
        };
    }

    private static CandidateFood ToCandidateFood(Contracts.CandidateFood food) => new()
    {
        FoodId = food.FoodId,
        Name = food.Name,
        ServingSize = food.ServingSize,
        ServingUnit = food.ServingUnit,
        Calories = food.Calories,
        ProteinGrams = food.ProteinGrams,
        CarbsGrams = food.CarbsGrams,
        FatGrams = food.FatGrams,
        FiberGrams = food.FiberGrams,
        Allergens = food.Allergens.ToList(),
    };

    private static Contracts.RecommendedMeal ToProtoMeal(MealSuggestion meal)
    {
        var message = new Contracts.RecommendedMeal
        {
            FoodId = meal.FoodId,
            Name = meal.Name,
            EstimatedCalories = meal.EstimatedCalories,
            ProteinGrams = meal.ProteinGrams,
            CarbsGrams = meal.CarbsGrams,
            FatGrams = meal.FatGrams,
            Rationale = meal.Rationale,
        };

        message.Citations.AddRange(meal.Citations.Select(ToProtoCitation));

        return message;
    }

    private static Contracts.Citation ToProtoCitation(Citation citation) => new()
    {
        ChapterNumber = citation.ChapterNumber,
        ChapterTitle = citation.ChapterTitle,
        BookPage = citation.BookPage,
        PdfPage = citation.PdfPage,
        Quote = citation.Quote,
        Verified = citation.Verified,
        SourceFile = citation.SourceFile,
    };
}
