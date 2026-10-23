namespace Identity.Domain.Constants;

public static class BiologicalSex
{
    public const string Male = "Male";
    public const string Female = "Female";

    public static readonly IReadOnlySet<string> All = new HashSet<string>
    {
        Male,
        Female,
    };
}
