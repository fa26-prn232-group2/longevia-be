namespace Recommendation.Infrastructure.Options;

/// <summary>
/// Cấu hình hạ tầng của Recommendation service.
/// Bind từ section <c>RECOMMENDATION</c> (env: <c>RECOMMENDATION__*</c>).
/// </summary>
public sealed class RecommendationOptions
{
    /// <summary>
    /// Tên section khớp tiền tố biến môi trường:
    /// <c>RECOMMENDATION__DEADLINE_SECONDS</c> -> section <c>RECOMMENDATION</c>.
    /// </summary>
    public const string SectionName = "RECOMMENDATION";

    /// <summary>Ngân sách thời gian xử lý phía server cho mỗi lời gọi gRPC (giây).</summary>
    /// <remarks>
    /// Phải lớn hơn độ trễ sinh văn bản thực tế của provider (Gemini chat đo được
    /// tới ~16s); nếu không, các câu trả lời dài sẽ bị <c>DEADLINE_EXCEEDED</c>.
    /// </remarks>
    public int DeadlineSeconds { get; set; } = 30;
}
