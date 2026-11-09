namespace Diet.Application.Common;

/// <summary>
/// Ngân sách thời gian cho các lời gọi Recommendation từ Diet.
/// Bind từ section <c>Recommendation</c> (env <c>RECOMMENDATION__*</c>).
/// </summary>
public sealed class RecommendationBudgetOptions
{
    /// <summary>Tên section khớp env prefix <c>RECOMMENDATION__</c>.</summary>
    public const string SectionName = "Recommendation";

    /// <summary>Giới hạn dưới để tránh cấu hình vô nghĩa (mili giây).</summary>
    public const int MinDeadlineMilliseconds = 1_000;

    /// <summary>Giới hạn trên để không giữ request quá lâu (mili giây).</summary>
    public const int MaxDeadlineMilliseconds = 60_000;

    /// <summary>
    /// Deadline cho một lời gọi gợi ý bữa ăn. Mặc định 20 giây vì provider
    /// Gemini đo được p95 tới ~16 giây; giá trị cũ 2.8 giây gây timeout.
    /// </summary>
    public int DeadlineMilliseconds { get; set; } = 20_000;

    public static bool IsValidDeadline(int milliseconds) =>
        milliseconds is >= MinDeadlineMilliseconds and <= MaxDeadlineMilliseconds;
}
