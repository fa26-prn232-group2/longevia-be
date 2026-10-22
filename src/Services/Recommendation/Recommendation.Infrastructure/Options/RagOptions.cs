namespace Recommendation.Infrastructure.Options;

/// <summary>
/// Cấu hình pipeline truy xuất tri thức (RAG).
/// Bind từ section <c>RAG</c> (env: <c>RAG__*</c>).
/// </summary>
public sealed class RagOptions
{
    /// <summary>
    /// Tên section khớp tiền tố biến môi trường: <c>RAG__TOP_K</c> -> section <c>RAG</c>.
    /// </summary>
    public const string SectionName = "RAG";

    /// <summary>Số đoạn trả về cho người dùng (mặc định 8, tối đa 20).</summary>
    public int TopK { get; set; } = 8;

    /// <summary>Số ứng viên của mỗi nhánh truy xuất trước khi merge bằng RRF.</summary>
    public int MaxCandidates { get; set; } = 50;

    /// <summary>Khi bật, chỉ dùng chunk đã đối chiếu bản in (<c>verified = true</c>).</summary>
    public bool RequireVerified { get; set; }

    /// <summary>Độ dài một chunk (token) khi nạp corpus.</summary>
    public int ChunkTokens { get; set; } = 1000;

    /// <summary>Độ chồng lấp giữa hai chunk liên tiếp (token).</summary>
    public int ChunkOverlap { get; set; } = 150;

    /// <summary>Ngưỡng tương đồng tối thiểu để một trích dẫn được giữ lại.</summary>
    public double MinCitationSimilarity { get; set; } = 0.75;

    /// <summary>
    /// Model nhúng dùng cho corpus. Đây là nguồn duy nhất của model embedding
    /// (runtime service và pipeline offline đều đọc từ đây) và phải khớp với
    /// cột <c>vector(n)</c> trong <c>kb.chunk</c>.
    /// </summary>
    public string EmbeddingModel { get; set; } = "gemini-embedding-001";

    /// <summary>
    /// Số chiều vector của corpus. Phải khớp cột <c>vector(n)</c> trong
    /// <c>kb.chunk</c>.
    /// </summary>
    public int EmbeddingDim { get; set; } = 768;
}
