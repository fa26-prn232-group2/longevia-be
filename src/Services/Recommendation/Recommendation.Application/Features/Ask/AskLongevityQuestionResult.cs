using Recommendation.Domain.Models;

namespace Recommendation.Application.Features.Ask;

/// <summary>
/// Kết quả hỏi tri thức trong sách: câu trả lời có căn cứ kèm trích dẫn đã kiểm
/// chứng, hoặc trạng thái abstain kèm lý do ổn định. Luôn kèm khuyến cáo (BR-04).
/// </summary>
public sealed record AskLongevityQuestionResult
{
    /// <summary>Câu trả lời tiếng Việt; là thông điệp cảnh báo khi abstain.</summary>
    public required string Answer { get; init; }

    /// <summary>Trích dẫn đã đối chiếu khớp nội dung đoạn nguồn.</summary>
    public IReadOnlyList<Citation> Citations { get; init; } = [];

    /// <summary>Không đủ căn cứ trong sách để trả lời.</summary>
    public bool Abstained { get; init; }

    /// <summary>Mã lý do abstain ổn định, ví dụ <c>no_matching_chunk</c>.</summary>
    public string? AbstainReason { get; init; }

    /// <summary>Khuyến cáo bắt buộc trên mọi phản hồi.</summary>
    public required string Disclaimer { get; init; }
}
