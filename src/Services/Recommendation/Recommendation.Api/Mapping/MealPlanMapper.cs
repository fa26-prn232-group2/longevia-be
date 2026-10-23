using Recommendation.Application.Features.MealPlans;
using Recommendation.Domain.Constants;
using Recommendation.Domain.Models;
using Contracts = Longevia.Recommendation.Contracts.V1;

namespace Recommendation.Api.Mapping;

/// <summary>
/// Map giữa message gRPC của <c>GenerateMealPlan</c> và DTO nội bộ của
/// Application. Contract type chỉ tồn tại ở tầng Api nên Application không tham
/// chiếu generated code.
/// </summary>
internal static class MealPlanMapper
{
    /// <summary>Chuyển request gRPC sang DTO Application.</summary>
    internal static GenerateMealPlanRequest ToApplicationRequest(Contracts.GenerateMealPlanRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new GenerateMealPlanRequest
        {
            Profile = ToProfile(request.Profile),
            TargetCalories = request.TargetCalories,
            TargetProteinGrams = request.TargetProteinGrams,
            TargetCarbsGrams = request.TargetCarbsGrams,
            TargetFatGrams = request.TargetFatGrams,
            TargetFiberGrams = request.TargetFiberGrams,
            WeekStartDate = request.WeekStartDate,
            Days = request.Days,
            MealsPerDay = request.MealsPerDay,
            CandidateFoods = request.CandidateFoods.Select(ToCandidateFood).ToList(),
            ExcludedFoodIds = request.ExcludedFoodIds.ToList(),
            ConsumedCalories = request.ConsumedCalories,
            ConsumedDate = string.IsNullOrWhiteSpace(request.ConsumedDate) ? null : request.ConsumedDate,
            Locale = request.Locale,
        };
    }

    /// <summary>Chuyển kết quả use case sang message phản hồi gRPC.</summary>
    internal static Contracts.GenerateMealPlanResponse ToProtoResponse(GenerateMealPlanResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        var response = new Contracts.GenerateMealPlanResponse
        {
            WeekStartDate = result.WeekStartDate,
            DaysRequested = result.DaysRequested,
            WeeklyTargetCalories = result.WeeklyTargetCalories,
            WeeklyTargetProteinGrams = result.WeeklyTargetProteinGrams,
            WeeklyTargetCarbsGrams = result.WeeklyTargetCarbsGrams,
            WeeklyTargetFatGrams = result.WeeklyTargetFatGrams,
            WeeklyTargetFiberGrams = result.WeeklyTargetFiberGrams,
            Disclaimer = new Contracts.Disclaimer { Text = result.Disclaimer },
            UsedFallback = result.UsedFallback,
        };

        response.Days.AddRange(result.Days.Select(ToProtoDay));
        response.Citations.AddRange(result.Citations.Select(ToProtoCitation));
        response.Warnings.AddRange(result.Warnings.Select(ToProtoWarning));

        return response;
    }

    private static UserProfile ToProfile(Contracts.UserProfile? profile)
    {
        if (profile is null)
        {
            return new UserProfile();
        }

        return new UserProfile
        {
            Age = profile.Age,
            Sex = profile.Sex switch
            {
                Contracts.BiologicalSex.Male => "MALE",
                Contracts.BiologicalSex.Female => "FEMALE",
                _ => string.Empty,
            },
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

    private static Contracts.MealPlanDay ToProtoDay(MealPlanDay day)
    {
        var message = new Contracts.MealPlanDay
        {
            Date = day.Date,
            TargetCalories = day.TargetCalories,
            TargetProteinGrams = day.TargetProteinGrams,
            TargetCarbsGrams = day.TargetCarbsGrams,
            TargetFatGrams = day.TargetFatGrams,
            TargetFiberGrams = day.TargetFiberGrams,
            TotalCalories = day.TotalCalories,
            TotalProteinGrams = day.TotalProteinGrams,
            TotalCarbsGrams = day.TotalCarbsGrams,
            TotalFatGrams = day.TotalFatGrams,
            TotalFiberGrams = day.TotalFiberGrams,
            CalorieCapReached = day.CalorieCapReached,
        };

        message.Meals.AddRange(day.Meals.Select(ToProtoMeal));
        message.Suggestions.AddRange(day.Suggestions.Select(ToProtoSuggestion));

        return message;
    }

    private static Contracts.PlannedMeal ToProtoMeal(PlannedMeal meal)
    {
        var message = new Contracts.PlannedMeal
        {
            MealType = ToProtoMealType(meal.MealType),
            TargetCalories = meal.TargetCalories,
            Rationale = meal.Rationale,
        };

        message.Foods.AddRange(meal.Foods.Select(ToProtoFood));
        message.Citations.AddRange(meal.Citations.Select(ToProtoCitation));

        return message;
    }

    private static Contracts.PlannedFood ToProtoFood(PlannedFood food)
    {
        var message = new Contracts.PlannedFood
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
        };

        message.Allergens.AddRange(food.Allergens);

        return message;
    }

    private static Contracts.PlanSuggestion ToProtoSuggestion(PlanSuggestion suggestion)
    {
        var message = new Contracts.PlanSuggestion { Text = suggestion.Text };
        message.Citations.AddRange(suggestion.Citations.Select(ToProtoCitation));
        return message;
    }

    private static Contracts.PlanWarning ToProtoWarning(PlanWarning warning) => new()
    {
        Code = warning.Code,
        Message = warning.Message,
        Scope = warning.Scope,
    };

    private static Contracts.MealType ToProtoMealType(MealSlot slot) => slot switch
    {
        MealSlot.Breakfast => Contracts.MealType.Breakfast,
        MealSlot.Lunch => Contracts.MealType.Lunch,
        MealSlot.Dinner => Contracts.MealType.Dinner,
        MealSlot.Snack => Contracts.MealType.Snack,
        _ => Contracts.MealType.Unspecified,
    };

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
