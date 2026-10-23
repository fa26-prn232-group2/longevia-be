using Recommendation.Domain.Models;

namespace Recommendation.Application.Features.Ask;

/// <summary>
/// Đầu vào của use case hỏi tri thức trong sách (Application DTO). Contract gRPC
/// được map sang kiểu này ở tầng Api; Application không tham chiếu generated code.
/// </summary>
public sealed record AskLongevityQuestionRequest
{
    /// <summary>Định danh người gọi — chỉ tồn tại trong luồng gọi, không được lưu.</summary>
    public string UserId { get; init; } = string.Empty;

    /// <summary>Câu hỏi của người dùng.</summary>
    public required string Question { get; init; }

    /// <summary>Hồ sơ người dùng của lời gọi này — không được lưu lại.</summary>
    public UserProfile Profile { get; init; } = new();

    /// <summary>Số chương cần lọc; <c>0</c> nghĩa là mọi chương.</summary>
    public int ChapterFilter { get; init; }
}
