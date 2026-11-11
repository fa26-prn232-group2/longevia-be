namespace Diet.Application.Abstractions;

public sealed record UserProfileSnapshot(DateOnly BirthDate, bool IsMale, decimal HeightCm,
    string ActivityLevel, string? DietaryPreference, IReadOnlyList<string>? Allergies,
    string? Goal = null)
{
    /// <summary>Người dùng chọn "Other": BMR/TDEE dùng hệ số trung tính.</summary>
    public bool IsOther { get; init; }

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
