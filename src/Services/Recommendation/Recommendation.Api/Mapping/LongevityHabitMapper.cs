using Recommendation.Application.Features.LongevityHabits;
using Recommendation.Domain.Models;
using Contracts = Longevia.Recommendation.Contracts.V1;

namespace Recommendation.Api.Mapping;

/// <summary>
/// Map giữa message gRPC của contract và DTO nội bộ của Application cho RPC đánh
/// giá thói quen trường thọ. Contract type chỉ tồn tại ở tầng Api nên Application
/// không tham chiếu generated code.
/// </summary>
internal static class LongevityHabitMapper
{
    /// <summary>Chuyển request gRPC sang DTO nội bộ; hồ sơ thiếu thì dùng mặc định rỗng.</summary>
    internal static LongevityHabitRequest ToApplicationRequest(Contracts.LongevityHabitRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new LongevityHabitRequest
        {
            UserId = request.UserId,
            Profile = ToProfile(request.Profile),
            AverageFastingHours = request.AverageFastingHours,
            AverageSleepHours = request.AverageSleepHours,
            WaterTargetHitDays = request.WaterTargetHitDays,
            NutritionAdherenceRate = request.NutritionAdherenceRate,
        };
    }

    /// <summary>Chuyển kết quả use case sang message phản hồi gRPC.</summary>
    internal static Contracts.LongevityHabitResponse ToProtoResponse(LongevityHabitResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        var response = new Contracts.LongevityHabitResponse
        {
            LongevityScore = result.LongevityScore,
            Disclaimer = new Contracts.Disclaimer { Text = result.Disclaimer },
        };

        response.ActionableInsights.AddRange(result.ActionableInsights);
        response.ScoreBreakdown.AddRange(result.Components.Select(ToProtoComponent));
        response.Citations.AddRange(result.Citations.Select(ToProtoCitation));

        return response;
    }

    private static Contracts.ScoreComponent ToProtoComponent(ScoreComponent component) => new()
    {
        Key = component.Key,
        Score = component.Score,
        Weight = component.Weight,
        Reason = component.Reason,
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
