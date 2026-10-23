namespace Recommendation.Application.Abstractions;

/// <summary>
/// Cổng gọi dịch vụ nhúng vector. Vector trả về luôn L2-normalized (norm = 1)
/// để khoảng cách cosine phản ánh đúng hướng (D-13).
/// </summary>
public interface IEmbeddingProvider
{
    /// <summary>Nhúng một câu truy vấn.</summary>
    Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken);

    /// <summary>Nhúng nhiều đoạn tài liệu trong một lời gọi (dùng cho công cụ nạp corpus).</summary>
    Task<IReadOnlyList<float[]>> EmbedBatchAsync(
        IReadOnlyList<string> texts,
        CancellationToken cancellationToken);
}
