using Longevia.Diet.Contracts.V1;
using Longevia.Recommendation.Contracts.V1;

namespace Progress.Application.Abstractions;

public interface IUserProfileProvider
{
    Task<UserProfileResult> GetAsync(Guid userId, CancellationToken ct);
}

public interface IUserContactProvider
{
    Task<UserContactResult> GetAsync(Guid userId, CancellationToken ct);
}

public sealed record UserContactResult(string Email, string FullName, string Status, bool ReceiveWeeklyDigest);

public sealed record UserProfileResult(int Age, bool IsMale, decimal HeightCm, string ActivityLevel,
    string DietaryPreference, IReadOnlyList<string> Allergies);

public interface IWorkoutPlanProvider
{
    Task<GenerateWorkoutPlanResponse> GenerateAsync(WorkoutPlanInput input, DateTimeOffset deadline,
        CancellationToken ct);
}

public sealed record WorkoutPlanInput(Guid UserId, UserProfileResult Profile, string Goal,
    decimal WeightKg, string WeekStartDate, int Days, int SessionsPerWeek, int MinutesPerSession,
    IReadOnlyList<string> ActivityPreference, IReadOnlyList<string> Equipment,
    IReadOnlyList<string> Limitations);

public interface ILongevityProvider
{
    Task<LongevityHabitResponse> EvaluateAsync(LongevityHabitRequest request, DateTimeOffset deadline,
        CancellationToken ct);
}

public interface INutritionAdherenceProvider
{
    Task<NutritionAdherenceResponse> GetAsync(Guid userId, DateOnly from, DateOnly to, CancellationToken ct);
}
