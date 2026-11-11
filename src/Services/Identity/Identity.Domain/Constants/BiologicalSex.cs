namespace Identity.Domain.Constants;

public static class BiologicalSex
{
    public const string Male = "Male";
    public const string Female = "Female";

    /// <summary>
    /// Người dùng không muốn tiết lộ giới tính sinh học. Các công thức BMR/TDEE dùng
    /// hệ số trung tính và ghi rõ đây chỉ là ước lượng.
    /// </summary>
    public const string Other = "Other";

    public static readonly IReadOnlySet<string> All = new HashSet<string>
    {
        Male,
        Female,
        Other,
    };
}
