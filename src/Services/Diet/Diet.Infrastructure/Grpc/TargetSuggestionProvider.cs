using Diet.Application.Abstractions;
using Diet.Application.Features.Targets;
using Grpc.Core;
using Longevia.Recommendation.Contracts.V1;
using Longevia.Shared.Errors;

namespace Diet.Infrastructure.Grpc;

public sealed class TargetSuggestionProvider(RecommendationService.RecommendationServiceClient client,
    TimeProvider time) : ITargetSuggestionProvider
{
    public async Task<TargetSuggestionResult> SuggestAsync(TargetSuggestionInput input,
        DateTimeOffset deadline, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (deadline <= time.GetUtcNow()) throw Timeout();

        var request = new SuggestNutritionTargetsRequest
        {
            Profile = new UserProfile
            {
                Age = input.Profile.AgeOn(DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime)),
                Sex = input.Profile.IsOther ? BiologicalSex.Other
                    : input.Profile.IsMale ? BiologicalSex.Male : BiologicalSex.Female,
                HeightCm = (double)input.Profile.HeightCm,
                WeightKg = (double)input.WeightKg,
                ActivityLevel = input.Profile.ActivityLevel,
                DietaryPreference = input.Profile.DietaryPreference ?? string.Empty,
            },
            Goal = input.Goal,
            Description = input.Description,
            CurrentCalories = input.CurrentCalories,
            Locale = "vi-VN",
        };
        request.Profile.Allergies.AddRange(input.Profile.Allergies ?? []);

        try
        {
            var response = await client.SuggestNutritionTargetsAsync(request,
                deadline: deadline.UtcDateTime, cancellationToken: ct).ResponseAsync;
            return new TargetSuggestionResult(
                response.Calories,
                (decimal)response.ProteinGrams,
                (decimal)response.CarbsGrams,
                (decimal)response.FatGrams,
                response.Rationale,
                response.Source,
                response.SafetyNote);
        }
        catch (RpcException) when (ct.IsCancellationRequested)
        {
            throw new OperationCanceledException(ct);
        }
        catch (RpcException ex)
        {
            throw ex.StatusCode switch
            {
                StatusCode.DeadlineExceeded => Timeout(),
                StatusCode.Unavailable => AppException.ServiceUnavailable(
                    "Gợi ý mục tiêu tạm thời không khả dụng.", "diet.ai_unavailable"),
                StatusCode.InvalidArgument => AppException.BadRequest(
                    "Hồ sơ chưa phù hợp để gợi ý mục tiêu.", "diet.invalid_profile"),
                _ => new AppException("Dịch vụ gợi ý trả dữ liệu không hợp lệ.", 502,
                    "Dịch vụ phụ thuộc trả dữ liệu không hợp lệ.", "diet.ai_integration_failed"),
            };
        }
    }

    private static AppException Timeout() =>
        new("Gợi ý mục tiêu quá thời gian chờ.", 504, "Dịch vụ phụ thuộc quá hạn.",
            "diet.ai_timeout");
}
