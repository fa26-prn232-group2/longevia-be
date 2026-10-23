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

    /// <summary>Giới hạn dưới của deadline server (giây).</summary>
    public const int MinDeadlineSeconds = 5;

    /// <summary>Giới hạn trên của deadline server (giây).</summary>
    public const int MaxDeadlineSeconds = 120;

    /// <summary>Deadline mặc định của 5 RPC hiện có (giây).</summary>
    public const int DefaultDeadlineSeconds = 30;

    /// <summary>Deadline mặc định của 2 RPC plan (giây).</summary>
    public const int DefaultPlanDeadlineSeconds = 90;

    /// <summary>Ngân sách thời gian xử lý phía server cho mỗi lời gọi gRPC (giây).</summary>
    /// <remarks>
    /// Phải lớn hơn độ trễ sinh văn bản thực tế của provider (Gemini chat đo được
    /// tới ~16s); nếu không, các câu trả lời dài sẽ bị <c>DEADLINE_EXCEEDED</c>.
    /// </remarks>
    public int DeadlineSeconds { get; set; } = DefaultDeadlineSeconds;

    /// <summary>
    /// Ngân sách thời gian xử lý riêng cho hai RPC plan
    /// (<c>GenerateMealPlan</c>/<c>GenerateWorkoutPlan</c>), giây.
    /// Mặc định 90, hợp lệ 5..120.
    /// </summary>
    public int PlanDeadlineSeconds { get; set; } = DefaultPlanDeadlineSeconds;

    /// <summary>
    /// <c>true</c> khi deadline nằm trong khoảng hợp lệ 5..120 giây.
    /// Dùng chung cho cả deadline cũ và deadline plan để tránh hai ngưỡng lệch nhau.
    /// </summary>
    public static bool IsValidDeadlineSeconds(int seconds) =>
        seconds is >= MinDeadlineSeconds and <= MaxDeadlineSeconds;
}
