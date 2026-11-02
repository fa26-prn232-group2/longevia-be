namespace Diet.Application.Abstractions;

public sealed record UserProfileSnapshot(DateOnly BirthDate, bool IsMale, decimal HeightCm,
    string ActivityLevel, string? DietaryPreference, IReadOnlyList<string>? Allergies)
{
    public int AgeOn(DateOnly today)
    {
        var age = today.Year - BirthDate.Year;
        return BirthDate > today.AddYears(-age) ? age - 1 : age;
    }
    public decimal ActivityFactor => ActivityLevel switch
    {
        "Sedentary" => 1.2m,
        "LightlyActive" => 1.375m,
        "ModeratelyActive" => 1.55m,
        "VeryActive" => 1.725m,
        _ => throw new InvalidOperationException("Unsupported activity level."),
    };
}

public interface IProfileProvider
{
    Task<UserProfileSnapshot> GetAsync(Guid userId, CancellationToken ct);
}
