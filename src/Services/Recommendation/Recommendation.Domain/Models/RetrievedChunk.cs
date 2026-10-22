namespace Recommendation.Domain.Models;

/// <summary>
/// Kết quả truy xuất: một đoạn tri thức kèm điểm liên quan đã gộp bằng RRF.
/// </summary>
public sealed record RetrievedChunk
{
    /// <summary>Đoạn được truy xuất.</summary>
    public required KnowledgeChunk Chunk { get; init; }

    /// <summary>Điểm liên quan (RRF) — lớn hơn là liên quan hơn.</summary>
    public required double Score { get; init; }
}
