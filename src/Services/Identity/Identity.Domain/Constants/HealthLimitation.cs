namespace Identity.Domain.Constants;

/// <summary>
/// Giới hạn sức khỏe canonical mà hồ sơ có thể khai. Chỉ nhận các rule đã được
/// Recommendation hỗ trợ để tránh warning <c>unsupported_limitation</c>.
/// </summary>
public static class HealthLimitation
{
    /// <summary>Đau/tổn thương khớp — loại hoạt động tác động mạnh.</summary>
    public const string JointPain = "joint_pain";

    /// <summary>Tập giá trị hợp lệ.</summary>
    public static readonly IReadOnlySet<string> All =
        new HashSet<string>(StringComparer.Ordinal) { JointPain };

    /// <summary>Số mục tối đa trong hồ sơ.</summary>
    public const int MaxItems = 8;
}
