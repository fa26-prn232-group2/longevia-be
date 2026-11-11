namespace Identity.Domain.Constants;

/// <summary>Mục tiêu sức khỏe dài hạn của người dùng.</summary>
public static class Goal
{
    public const string LoseWeight = "LoseWeight";
    public const string Maintain = "Maintain";
    public const string BuildMuscle = "BuildMuscle";
    public const string Longevity = "Longevity";

    public static readonly IReadOnlySet<string> All = new HashSet<string>
    {
        LoseWeight,
        Maintain,
        BuildMuscle,
        Longevity,
    };
}
