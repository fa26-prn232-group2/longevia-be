using Recommendation.Application.Features.NutrientGaps;
using Recommendation.Domain.Models;
using Contracts = Longevia.Recommendation.Contracts.V1;

namespace Recommendation.Api.Mapping;

/// <summary>
/// Map giữa message gRPC của contract và DTO nội bộ của Application cho RPC phân
/// tích vi chất. Contract type chỉ tồn tại ở tầng Api nên Application không tham
/// chiếu generated code.
/// </summary>
internal static class NutrientGapMapper
{
    /// <summary>Chuyển request gRPC sang DTO nội bộ; hồ sơ thiếu thì dùng mặc định rỗng.</summary>
    internal static NutrientGapRequest ToApplicationRequest(Contracts.NutrientGapRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new NutrientGapRequest
        {
            UserId = request.UserId,
            Profile = ToProfile(request.Profile),
            Past7Days = request.Past7Days.Select(ToDailySummary).ToList(),
            Targets = new NutrientTargets
            {
                TargetCalories = request.TargetCalories,
                TargetProteinGrams = request.TargetProteinGrams,
                TargetCarbsGrams = request.TargetCarbsGrams,
                TargetFatGrams = request.TargetFatGrams,
                TargetFiberGrams = request.TargetFiberGrams,
            },
        };
    }

    /// <summary>Chuyển kết quả use case sang message phản hồi gRPC.</summary>
    internal static Contracts.NutrientGapResponse ToProtoResponse(NutrientGapResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        var response = new Contracts.NutrientGapResponse
        {
            AnalysisSummary = result.AnalysisSummary,
            Disclaimer = new Contracts.Disclaimer { Text = result.Disclaimer },
        };

        response.Gaps.AddRange(result.Gaps.Select(ToProtoGap));
        response.DeficientNutrients.AddRange(result.DeficientNutrients);
        response.SuggestedFoods.AddRange(result.SuggestedFoods);
        response.Citations.AddRange(result.Citations.Select(ToProtoCitation));

        return response;
    }

    private static DailyNutrientSummary ToDailySummary(Contracts.DailyNutrientSummary summary) => new()
    {
        Date = summary.Date,
        Calories = summary.Calories,
        Protein = summary.Protein,
        Carbs = summary.Carbs,
        Fat = summary.Fat,
        Fiber = summary.Fiber,
        Micronutrients = summary.Micronutrients.Select(ToMicronutrient).ToList(),
    };

    private static MicronutrientIntake ToMicronutrient(Contracts.MicronutrientIntake intake) => new()
    {
        Nutrient = intake.Nutrient,
        Unit = intake.Unit,
        Amount = intake.Amount,
    };

    private static Contracts.NutrientGap ToProtoGap(NutrientGap gap) => new()
    {
        Nutrient = gap.Nutrient,
        Unit = gap.Unit,
        Target = gap.Target,
        AverageIntake = gap.AverageIntake,
        Deficit = gap.Deficit,
        Severity = gap.Severity,
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
