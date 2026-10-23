namespace Recommendation.Application.Features.Search;

/// <summary>
/// Đầu vào của use case tra cứu tri thức trong sách (Application DTO). Contract
/// gRPC được map sang kiểu này ở tầng Api; Application không tham chiếu
/// generated code.
/// </summary>
public sealed record SearchKnowledgeRequest
{
    /// <summary>Truy vấn tự do của người dùng.</summary>
    public required string Query { get; init; }

    /// <summary>Số chương cần lọc; <c>0</c> nghĩa là mọi chương.</summary>
    public int ChapterFilter { get; init; }

    /// <summary>Số đoạn cần trả về; <c>0</c> nghĩa là dùng mặc định 8.</summary>
    public int TopK { get; init; }
}
