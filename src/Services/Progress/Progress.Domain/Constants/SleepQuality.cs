namespace Progress.Domain.Constants;

public static class SleepQuality
{
    public const string Poor = "Poor";
    public const string Fair = "Fair";
    public const string Good = "Good";
    public const string Excellent = "Excellent";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        Poor,
        Fair,
        Good,
        Excellent
    };
}
