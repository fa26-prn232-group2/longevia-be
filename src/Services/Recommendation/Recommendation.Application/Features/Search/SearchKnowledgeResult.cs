namespace Recommendation.Application.Features.Search;

/// <summary>
/// Kết quả tra cứu tri thức trong sách: danh sách đoạn đã sắp xếp theo độ liên
/// quan giảm dần. Rỗng nghĩa là không có đoạn nào khớp truy vấn.
/// </summary>
public sealed record SearchKnowledgeResult
{
    /// <summary>Các đoạn khớp truy vấn, liên quan nhất trước.</summary>
    public IReadOnlyList<KnowledgeSnippet> Snippets { get; init; } = [];
}
