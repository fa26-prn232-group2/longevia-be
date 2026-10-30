namespace Progress.Domain.Constants;

public static class FastingProtocol
{
    public const string Protocol16_8 = "16:8";
    public const string Protocol18_6 = "18:6";
    public const string Protocol20_4 = "20:4";
    public const string Custom = "Custom";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        Protocol16_8,
        Protocol18_6,
        Protocol20_4,
        Custom
    };

    public static bool TryGetPresetTargetHours(string protocol, out int targetHours)
    {
        switch (protocol)
        {
            case Protocol16_8:
                targetHours = 16;
                return true;
            case Protocol18_6:
                targetHours = 18;
                return true;
            case Protocol20_4:
                targetHours = 20;
                return true;
            default:
                targetHours = 0;
                return false;
        }
    }
}
