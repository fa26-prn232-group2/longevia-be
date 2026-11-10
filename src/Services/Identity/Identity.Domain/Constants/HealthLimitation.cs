namespace Identity.Domain.Constants;

/// <summary>
/// Giới hạn sức khỏe là ngữ cảnh tự do cho LLM và bác sĩ/người dùng; không còn
/// allowlist cứng. Chỉ giữ giới hạn số lượng và độ dài để dữ liệu sạch.
/// </summary>
public static class HealthLimitation
{
    /// <summary>Số mục tối đa trong hồ sơ.</summary>
    public const int MaxItems = 8;

    /// <summary>Độ dài tối đa của một mục.</summary>
    public const int MaxLength = 50;
}
