namespace Identity.Domain.Constants;

public static class UserStatus
{
    public const string PendingVerification = "PendingVerification";
    public const string Active = "Active";
    public const string Suspended = "Suspended";

    public static readonly IReadOnlySet<string> All = new HashSet<string>
    {
        PendingVerification,
        Active,
        Suspended,
    };
}
