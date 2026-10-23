namespace Recommendation.Domain.Models;

/// <summary>
/// Prompt gửi tới mô hình ngôn ngữ: chỉ dẫn hệ thống, nội dung người dùng và
/// loại phản hồi có cấu trúc cần sinh.
/// </summary>
public sealed record StructuredPrompt
{
    /// <summary>Chỉ dẫn hệ thống (3 tầng ràng buộc của RAG).</summary>
    public required string SystemInstruction { get; init; }

    /// <summary>Nội dung gửi kèm: câu hỏi và ngữ cảnh đã truy xuất.</summary>
    public required string UserPrompt { get; init; }

    /// <summary>Loại phản hồi có cấu trúc cần sinh.</summary>
    public required StructuredResponseKind ResponseKind { get; init; }
}
