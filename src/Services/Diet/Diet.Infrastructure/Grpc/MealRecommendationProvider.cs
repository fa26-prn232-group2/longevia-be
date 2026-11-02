using System.Globalization;
using Diet.Application.Abstractions;
using Grpc.Core;
using Longevia.Recommendation.Contracts.V1;
using Longevia.Shared.Errors;

namespace Diet.Infrastructure.Grpc;

public sealed class MealRecommendationProvider(RecommendationService.RecommendationServiceClient client,
    TimeProvider time) : IMealRecommendationProvider
{
    public async Task<MealRecommendationResult> GetAsync(MealRecommendationInput input,
        DateTimeOffset deadline, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (input.UserId == Guid.Empty || input.MealType is < 1 or > 4 || input.Goal is < 1 or > 4 ||
            input.TargetCalories <= 0 || input.ConsumedCalories < 0 || input.Foods.Count > 100 ||
            input.Profile.Age is < 1 or > 120 || input.Profile.HeightCm is < 50 or > 250 ||
            input.Profile.WeightKg is < 25 or > 300 || input.Profile.Allergies is null ||
            string.IsNullOrWhiteSpace(input.Profile.ActivityLevel) || string.IsNullOrWhiteSpace(input.Profile.DietaryPreference))
            throw new ArgumentException("Invalid meal recommendation context.", nameof(input));
        var now = time.GetUtcNow();
        var expires = deadline < now.AddSeconds(2) ? deadline : now.AddSeconds(2);
        if (expires <= now) throw Timeout();
        var foods = input.Foods.Where(x => x.Allergens is not null &&
            (x.UserId is null || x.UserId == input.UserId) &&
            !x.Allergens.Any(a => input.Profile.Allergies.Any(b =>
                string.Equals(a, b.Trim(), StringComparison.OrdinalIgnoreCase)))).ToDictionary(x => x.Id);
        var request = new MealRecommendationRequest
        {
            UserId = input.UserId.ToString("D"),
            MealType = (MealType)input.MealType,
            Goal = (UserGoal)input.Goal,
            Profile = new UserProfile
            {
                Age = input.Profile.Age,
                Sex = input.Profile.IsMale ? BiologicalSex.Male : BiologicalSex.Female,
                HeightCm = (double)input.Profile.HeightCm,
                WeightKg = (double)input.Profile.WeightKg,
                ActivityLevel = input.Profile.ActivityLevel,
                DietaryPreference = input.Profile.DietaryPreference
            },
            ConsumedCalories = input.ConsumedCalories,
            TargetCalories = input.TargetCalories,
            TargetProteinRemaining = (double)Math.Max(0, input.ProteinRemaining),
            TargetCarbsRemaining = (double)Math.Max(0, input.CarbsRemaining),
            TargetFatRemaining = (double)Math.Max(0, input.FatRemaining),
            Locale = "vi-VN",
        };
        request.Profile.Allergies.AddRange(input.Profile.Allergies);
        foreach (var food in foods.Values)
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
                FiberGrams = (double)food.Nutrition.FiberGrams
            };
            candidate.Allergens.AddRange(food.Allergens!);
            request.CandidateFoods.Add(candidate);
        }
        MealRecommendationResponse reply;
        try
        {
            reply = await client.GetMealRecommendationsAsync(request, deadline: expires.UtcDateTime,
                cancellationToken: ct).ResponseAsync;
        }
        catch (RpcException) when (ct.IsCancellationRequested) { throw new OperationCanceledException(ct); }
        catch (RpcException ex)
        {
            throw ex.StatusCode switch
            {
                StatusCode.DeadlineExceeded => Timeout(),
                StatusCode.Unavailable => AppException.ServiceUnavailable("Gợi ý bữa ăn tạm thời không khả dụng.", "diet.ai_unavailable"),
                _ => InvalidResponse(),
            };
        }
        if (string.IsNullOrWhiteSpace(reply.Disclaimer?.Text) || reply.Meals.Count > 5 ||
            (!string.IsNullOrWhiteSpace(reply.LongevityTip) && reply.TipCitations.Count == 0)) throw InvalidResponse();
        var suggestions = new List<MealSuggestion>();
        var seen = new HashSet<Guid>();
        foreach (var meal in reply.Meals)
        {
            // Recommendation emits a non-food relaxation suggestion when the calorie cap is reached.
            if (reply.CalorieCapReached && string.IsNullOrEmpty(meal.FoodId)) continue;
            if (!Guid.TryParse(meal.FoodId, out var id) || !foods.TryGetValue(id, out var food) ||
                !seen.Add(id) || meal.Citations.Count == 0) throw InvalidResponse();
            suggestions.Add(new(id, food.Name, food.ServingSize, food.ServingUnit, food.Nutrition.Calories,
                food.Nutrition.ProteinGrams, food.Nutrition.CarbsGrams, food.Nutrition.FatGrams,
                meal.Description, meal.Rationale, meal.Citations.Select(MapCitation).ToArray()));
        }
        return new(suggestions, reply.LongevityTip, reply.TipCitations.Select(MapCitation).ToArray(),
            reply.Disclaimer.Text, reply.CalorieCapReached, reply.SafetyNote);
    }

    private static MealCitation MapCitation(Citation citation)
    {
        if (string.IsNullOrWhiteSpace(citation.Quote) || string.IsNullOrWhiteSpace(citation.SourceFile)) throw InvalidResponse();
        return new(citation.ChapterNumber, citation.ChapterTitle, citation.BookPage, citation.PdfPage,
            citation.Quote, citation.Verified, citation.SourceFile);
    }
    private static AppException InvalidResponse() => new("Chưa thể sử dụng kết quả gợi ý.", 502,
        "Lỗi dịch vụ phụ thuộc.", "diet.ai_integration_failed");
    private static AppException Timeout() => new("Gợi ý bữa ăn quá thời gian chờ.", 504,
        "Dịch vụ phụ thuộc quá hạn.", "diet.ai_timeout");
}
