using Identity.Domain.Constants;

namespace Identity.Domain.Entities;

public sealed class UserProfile
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public DateOnly BirthDate { get; private set; }
    public string BiologicalSex { get; private set; } = Constants.BiologicalSex.Male;
    public int HeightCm { get; private set; }
    public string ActivityLevel { get; private set; } = Constants.ActivityLevel.Sedentary;
    public string DietaryPreference { get; private set; } = Constants.DietaryPreference.None;
    public List<string> Allergies { get; private set; } = [];
    public DateTimeOffset UpdatedAt { get; private set; }

    private UserProfile()
    {
    }

    public static UserProfile Create(
        Guid id,
        Guid userId,
        DateOnly birthDate,
        string biologicalSex,
        int heightCm,
        string activityLevel,
        string dietaryPreference,
        IEnumerable<string>? allergies,
        DateTimeOffset now)
    {
        Validate(heightCm, biologicalSex, activityLevel, dietaryPreference);

        return new UserProfile
        {
            Id = id,
            UserId = userId,
            BirthDate = birthDate,
            BiologicalSex = biologicalSex,
            HeightCm = heightCm,
            ActivityLevel = activityLevel,
            DietaryPreference = dietaryPreference,
            Allergies = allergies?.Where(a => !string.IsNullOrWhiteSpace(a)).Select(a => a.Trim()).Distinct().ToList() ?? [],
            UpdatedAt = now,
        };
    }

    public void Update(
        DateOnly birthDate,
        string biologicalSex,
        int heightCm,
        string activityLevel,
        string dietaryPreference,
        IEnumerable<string>? allergies,
        DateTimeOffset now)
    {
        Validate(heightCm, biologicalSex, activityLevel, dietaryPreference);

        BirthDate = birthDate;
        BiologicalSex = biologicalSex;
        HeightCm = heightCm;
        ActivityLevel = activityLevel;
        DietaryPreference = dietaryPreference;
        Allergies = allergies?.Where(a => !string.IsNullOrWhiteSpace(a)).Select(a => a.Trim()).Distinct().ToList() ?? [];
        UpdatedAt = now;
    }

    private static void Validate(
        int heightCm,
        string biologicalSex,
        string activityLevel,
        string dietaryPreference)
    {
        if (heightCm is < 50 or > 250)
        {
            throw new ArgumentOutOfRangeException(nameof(heightCm), "Chiều cao phải từ 50cm đến 250cm.");
        }

        if (!Constants.BiologicalSex.All.Contains(biologicalSex))
        {
            throw new ArgumentException($"Giới tính không hợp lệ: {biologicalSex}.", nameof(biologicalSex));
        }

        if (!Constants.ActivityLevel.All.Contains(activityLevel))
        {
            throw new ArgumentException($"Mức độ vận động không hợp lệ: {activityLevel}.", nameof(activityLevel));
        }

        if (!Constants.DietaryPreference.All.Contains(dietaryPreference))
        {
            throw new ArgumentException($"Chế độ ăn không hợp lệ: {dietaryPreference}.", nameof(dietaryPreference));
        }
    }
}
