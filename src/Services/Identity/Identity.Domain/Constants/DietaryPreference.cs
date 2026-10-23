namespace Identity.Domain.Constants;

public static class DietaryPreference
{
    public const string None = "None";
    public const string Keto = "Keto";
    public const string Vegan = "Vegan";
    public const string Mediterranean = "Mediterranean";
    public const string LowCarb = "LowCarb";

    public static readonly IReadOnlySet<string> All = new HashSet<string>
    {
        None,
        Keto,
        Vegan,
        Mediterranean,
        LowCarb,
    };
}
