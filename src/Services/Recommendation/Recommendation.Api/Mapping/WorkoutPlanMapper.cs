using Recommendation.Application.Features.WorkoutPlans;
using Recommendation.Domain.Models;
using Contracts = Longevia.Recommendation.Contracts.V1;

namespace Recommendation.Api.Mapping;

/// <summary>
/// Map giữa message gRPC của <c>GenerateWorkoutPlan</c> và DTO nội bộ của
/// Application. Contract type chỉ tồn tại ở tầng Api nên Application không tham
/// chiếu generated code.
/// </summary>
internal static class WorkoutPlanMapper
{
    /// <summary>Chuyển request gRPC sang DTO Application.</summary>
    internal static GenerateWorkoutPlanRequest ToApplicationRequest(Contracts.GenerateWorkoutPlanRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new GenerateWorkoutPlanRequest
        {
            Profile = ToProfile(request.Profile),
            Goal = request.Goal switch
            {
                Contracts.UserGoal.LoseWeight => "LOSE_WEIGHT",
                Contracts.UserGoal.Maintain => "MAINTAIN",
                Contracts.UserGoal.BuildMuscle => "BUILD_MUSCLE",
                Contracts.UserGoal.Longevity => "LONGEVITY",
                _ => string.Empty,
            },
            WeekStartDate = request.WeekStartDate,
            Days = request.Days,
            SessionsPerWeek = request.SessionsPerWeek,
            MinutesPerSession = request.MinutesPerSession,
            ActivityPreference = request.ActivityPreference.ToList(),
            EquipmentAvailable = request.EquipmentAvailable.ToList(),
            Limitations = request.Limitations.ToList(),
            Locale = request.Locale,
        };
    }

    /// <summary>Chuyển kết quả use case sang message phản hồi gRPC.</summary>
    internal static Contracts.GenerateWorkoutPlanResponse ToProtoResponse(GenerateWorkoutPlanResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        var response = new Contracts.GenerateWorkoutPlanResponse
        {
            WeekStartDate = result.WeekStartDate,
            DaysRequested = result.DaysRequested,
            SessionsScheduled = result.SessionsScheduled,
            MinutesPerSession = result.MinutesPerSession,
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

    private static Contracts.WorkoutPlanDay ToProtoDay(WorkoutPlanDay day)
    {
        var message = new Contracts.WorkoutPlanDay
        {
            Date = day.Date,
            RestDay = day.RestDay,
            Activity = day.Activity,
            DurationMinutes = day.DurationMinutes,
            Intensity = day.Intensity,
            Rationale = day.Rationale,
        };

        message.EquipmentUsed.AddRange(day.EquipmentUsed);
        message.Citations.AddRange(day.Citations.Select(ToProtoCitation));

        return message;
    }

    private static Contracts.PlanWarning ToProtoWarning(PlanWarning warning) => new()
    {
        Code = warning.Code,
        Message = warning.Message,
        Scope = warning.Scope,
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
