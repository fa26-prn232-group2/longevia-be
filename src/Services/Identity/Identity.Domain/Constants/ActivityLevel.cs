namespace Identity.Domain.Constants;

public static class ActivityLevel
{
    public const string Sedentary = "Sedentary";
    public const string LightlyActive = "LightlyActive";
    public const string ModeratelyActive = "ModeratelyActive";
    public const string VeryActive = "VeryActive";

    public static readonly IReadOnlySet<string> All = new HashSet<string>
    {
        Sedentary,
        LightlyActive,
        ModeratelyActive,
        VeryActive,
    };
}
