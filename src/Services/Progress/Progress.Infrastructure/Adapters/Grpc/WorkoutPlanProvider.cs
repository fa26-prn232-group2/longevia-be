using Longevia.Recommendation.Contracts.V1;
using Progress.Application.Abstractions;

namespace Progress.Infrastructure.Adapters.Grpc;

public sealed class WorkoutPlanProvider(RecommendationService.RecommendationServiceClient client) : IWorkoutPlanProvider
{
    public async Task<GenerateWorkoutPlanResponse> GenerateAsync(WorkoutPlanInput input,
        DateTimeOffset deadline, CancellationToken ct)
    {
        var request = new GenerateWorkoutPlanRequest
        {
            Profile = new UserProfile
            {
                Age = input.Profile.Age,
                Sex = input.Profile.IsOther ? BiologicalSex.Other
                    : input.Profile.IsMale ? BiologicalSex.Male : BiologicalSex.Female,
                HeightCm = (double)input.Profile.HeightCm,
                WeightKg = (double)input.WeightKg,
                ActivityLevel = input.Profile.ActivityLevel,
                DietaryPreference = input.Profile.DietaryPreference,
            },
            Goal = input.Goal switch
            {
                "LoseWeight" => UserGoal.LoseWeight,
                "Maintain" => UserGoal.Maintain,
                "BuildMuscle" => UserGoal.BuildMuscle,
                _ => UserGoal.Longevity,
            },
            WeekStartDate = input.WeekStartDate,
            Days = input.Days,
            SessionsPerWeek = input.SessionsPerWeek,
            MinutesPerSession = input.MinutesPerSession,
            Locale = "vi-VN",
        };
        request.Profile.Allergies.AddRange(input.Profile.Allergies);
        request.ActivityPreference.AddRange(input.ActivityPreference);
        request.EquipmentAvailable.AddRange(input.Equipment);
        request.Limitations.AddRange(input.Limitations);
        return await client.GenerateWorkoutPlanAsync(request, deadline: deadline.UtcDateTime,
            cancellationToken: ct).ResponseAsync;
    }
}
