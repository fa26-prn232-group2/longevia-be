using Recommendation.Domain.Models;

namespace Recommendation.Application.Features.LongevityHabits;

/// <summary>
/// Kết quả đánh giá thói quen trường thọ: điểm và breakdown do code tính, gợi ý
/// hành động có căn cứ sách (hoặc tip tĩnh khi corpus không có nguồn), trích dẫn
/// đã kiểm chứng và khuyến cáo bắt buộc.
/// </summary>
public sealed record LongevityHabitResult
{
    /// <summary>Điểm thói quen trường thọ 0..100, deterministic.</summary>
    public required int LongevityScore { get; init; }

    /// <summary>Bốn thành phần điểm kèm trọng số và lý do.</summary>
    public required IReadOnlyList<ScoreComponent> Components { get; init; }

    /// <summary>Gợi ý hành động; rỗng khi mô hình không trả được nội dung dùng được.</summary>
    public IReadOnlyList<string> ActionableInsights { get; init; } = [];

    /// <summary>Trích dẫn đã kiểm chứng khớp nội dung đoạn nguồn.</summary>
    public IReadOnlyList<Citation> Citations { get; init; } = [];

    /// <summary>Khuyến cáo bắt buộc trên mọi phản hồi (FR-027).</summary>
    public required string Disclaimer { get; init; }
}
