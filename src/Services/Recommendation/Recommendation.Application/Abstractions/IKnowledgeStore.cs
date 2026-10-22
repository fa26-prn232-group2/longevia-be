using Recommendation.Domain.Models;

namespace Recommendation.Application.Abstractions;

/// <summary>
/// Cổng truy cập kho tri thức đã nhúng (<c>recommendation_db</c>, schema <c>kb</c>).
/// Đường đọc là bắt buộc; đường ghi chỉ dành cho công cụ kiểm thử — pipeline
/// nạp corpus thật chạy offline (D-02), runtime service chỉ đọc.
/// </summary>
public interface IKnowledgeStore
{
    /// <summary>Truy xuất hybrid (vector + keyword, merge bằng RRF) theo truy vấn.</summary>
    Task<IReadOnlyList<RetrievedChunk>> SearchAsync(KnowledgeQuery query, CancellationToken cancellationToken);

    /// <summary>Thêm hoặc cập nhật đoạn theo khoá <c>(CorpusId, ChapterId, ChunkIndex)</c>.</summary>
    Task<int> UpsertAsync(IReadOnlyList<KnowledgeChunk> chunks, CancellationToken cancellationToken);

    /// <summary>Đếm số đoạn của corpus đang hoạt động.</summary>
    Task<int> CountAsync(string corpusKey, CancellationToken cancellationToken);
}
