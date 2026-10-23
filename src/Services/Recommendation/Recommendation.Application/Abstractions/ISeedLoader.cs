namespace Recommendation.Application.Abstractions;

/// <summary>
/// Nạp seed tri thức đã xuất sẵn (<c>build.json</c> + JSONL nén) vào
/// <c>recommendation_db</c> mà không cần gọi provider hay API key (D-15).
/// Thao tác ghi idempotent theo khoá tự nhiên của từng bảng và chỉ dành cho
/// bootstrap/deploy, không chạy trong request runtime.
/// </summary>
public interface ISeedLoader
{
    /// <summary>
    /// Nạp seed tại <see cref="SeedLoadOptions.SeedPath"/>; toàn bộ thao tác ghi nằm
    /// trong một transaction. Ném <see cref="SeedLoadException"/> khi seed không hợp lệ
    /// hoặc verify không đạt.
    /// </summary>
    Task<SeedLoadResult> LoadAsync(SeedLoadOptions options, CancellationToken cancellationToken = default);
}

/// <summary>Seed không hợp lệ, không khớp cấu hình corpus, hoặc verify thất bại.</summary>
public sealed class SeedLoadException : Exception
{
    public SeedLoadException(string message)
        : base(message)
    {
    }

    public SeedLoadException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
