using System.Globalization;
using Diet.Application.Abstractions;
using Grpc.Core;
using Longevia.Recommendation.Contracts.V1;
using Longevia.Shared.Errors;

namespace Diet.Infrastructure.Grpc;

public sealed class MealPlanProvider(RecommendationService.RecommendationServiceClient client,
    TimeProvider time) : IMealPlanProvider
{
    public async Task<GenerateMealPlanResponse> GenerateAsync(MealPlanInput input, DateTimeOffset deadline,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var now = time.GetUtcNow();
        if (deadline <= now) throw Timeout();
        if (input.TargetCalories <= 0 || input.TargetProteinGrams < 0 || input.TargetCarbsGrams < 0 ||
            input.TargetFatGrams < 0 || input.Foods.Count == 0)
            throw new ArgumentException("Invalid meal-plan context.", nameof(input));

        var request = new GenerateMealPlanRequest
        {
            Profile = new UserProfile
            {
                Age = input.Profile.Age,
                Sex = input.Profile.IsOther ? BiologicalSex.Other
                    : input.Profile.IsMale ? BiologicalSex.Male : BiologicalSex.Female,
                HeightCm = (double)input.Profile.HeightCm,
                WeightKg = (double)input.Profile.WeightKg,
                ActivityLevel = input.Profile.ActivityLevel,
                DietaryPreference = input.Profile.DietaryPreference,
            },
            TargetCalories = input.TargetCalories,
            TargetProteinGrams = input.TargetProteinGrams,
            TargetCarbsGrams = input.TargetCarbsGrams,
            TargetFatGrams = input.TargetFatGrams,
            TargetFiberGrams = input.TargetFiberGrams,
            WeekStartDate = input.WeekStartDate,
            Days = input.Days,
            MealsPerDay = input.MealsPerDay,
            ConsumedCalories = input.ConsumedCalories,
            ConsumedDate = input.ConsumedDate ?? string.Empty,
            Locale = "vi-VN",
        };
        request.Profile.Allergies.AddRange(input.Profile.Allergies);
        request.ExcludedFoodIds.AddRange(input.ExcludedFoodIds.Select(x => x.ToString("D")));
        foreach (var food in input.Foods)
        {
            var candidate = new CandidateFood
            {
                FoodId = food.Id.ToString("D"),
                Name = food.Name,
                ServingSize = food.ServingSize.ToString(CultureInfo.InvariantCulture),
                ServingUnit = food.ServingUnit,
                Calories = checked((int)decimal.Round(food.Nutrition.Calories, 0, MidpointRounding.AwayFromZero)),
                ProteinGrams = (double)food.Nutrition.ProteinGrams,
                CarbsGrams = (double)food.Nutrition.CarbsGrams,
                FatGrams = (double)food.Nutrition.FatGrams,
                FiberGrams = (double)food.Nutrition.FiberGrams,
                Category = food.Category,
            };
            candidate.Allergens.AddRange(food.Allergens ?? []);
            request.CandidateFoods.Add(candidate);
        }

        try
        {
            return await client.GenerateMealPlanAsync(request, deadline: deadline.UtcDateTime,
                cancellationToken: ct).ResponseAsync;
        }
        catch (RpcException) when (ct.IsCancellationRequested) { throw new OperationCanceledException(ct); }
        catch (RpcException ex)
        {
            throw ex.StatusCode switch
            {
                StatusCode.DeadlineExceeded => Timeout(),
                StatusCode.Unavailable => AppException.ServiceUnavailable(
                    "Kế hoạch ăn uống tạm thời không khả dụng.", "diet.ai_unavailable"),
                StatusCode.InvalidArgument => AppException.BadRequest(
                    "Không thể tạo kế hoạch với dữ liệu hiện tại.", "diet.invalid_meal_plan"),
                _ => new AppException("Dịch vụ gợi ý trả dữ liệu không hợp lệ.", 502,
                    "Dịch vụ phụ thuộc trả dữ liệu không hợp lệ.", "diet.ai_integration_failed"),
            };
        }
    }

    private static AppException Timeout() =>
        new("Tạo kế hoạch ăn uống quá thời gian chờ.", 504, "Dịch vụ phụ thuộc quá hạn.",
            "diet.ai_timeout");
}
