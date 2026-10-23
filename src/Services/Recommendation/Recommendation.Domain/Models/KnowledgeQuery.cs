namespace Recommendation.Domain.Models;

/// <summary>
/// Truy vấn truy xuất tri thức: câu hỏi đã chuẩn hoá (chưa bỏ dấu — việc bỏ dấu
/// do <c>unaccent</c> phía PostgreSQL làm), bộ lọc chương và số đoạn cần trả.
/// </summary>
public sealed record KnowledgeQuery
{
    /// <summary>Câu hỏi/truy vấn của người dùng.</summary>
    public required string Query { get; init; }

    /// <summary>Số chương cần lọc; <c>0</c> nghĩa là mọi chương.</summary>
    public int ChapterNumber { get; init; }

    /// <summary>Số đoạn cần trả; <c>0</c> nghĩa là dùng <c>RAG__TOP_K</c>.</summary>
    public int TopK { get; init; }

    /// <summary>Chỉ dùng đoạn đã đối chiếu bản in khi <c>true</c>.</summary>
    public bool RequireVerified { get; init; }
}
