namespace Recommendation.Domain.Constants;

/// <summary>
/// Khuyến cáo bắt buộc trên mọi response (FR-027 / BR-04): nội dung chỉ mang
/// tính tham khảo khoa học, không thay thế tư vấn của bác sĩ chuyên khoa.
/// </summary>
public static class DisclaimerText
{
    /// <summary>Nội dung khuyến cáo trả về trong trường <c>disclaimer.text</c>.</summary>
    public const string Value =
        "Nội dung chỉ mang tính chất tham khảo khoa học, dựa trên sách tham chiếu và không thay thế "
        + "tư vấn của bác sĩ chuyên khoa. Hãy tham vấn chuyên gia y tế trước khi thay đổi chế độ ăn, "
        + "đặc biệt nếu bạn có bệnh nền, đang dùng thuốc hoặc đang mang thai.";
}
