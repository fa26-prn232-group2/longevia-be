namespace Recommendation.Infrastructure.Options;

/// <summary>
/// Cấu hình knowledge store của Recommendation (<c>recommendation_db</c>, schema <c>kb</c>).
/// Theo Constitution §I, database này chỉ chứa corpus tri thức, không chứa dữ liệu người dùng.
/// </summary>
public sealed class KnowledgeDbOptions
{
    /// <summary>
    /// Tên section khớp tiền tố biến môi trường: <c>KNOWLEDGE_DB__*</c> -> section
    /// <c>KNOWLEDGE_DB</c>.
    /// </summary>
    public const string SectionName = "KNOWLEDGE_DB";

    /// <summary>
    /// Connection string tới <c>recommendation_db</c>. Lấy từ
    /// <c>ConnectionStrings__Recommendation</c>.
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>Khoá của corpus đang hoạt động (mỗi corpus một khoá).</summary>
    public string CorpusKey { get; set; } = "che-do-an-truong-tho";

    /// <summary>Schema chứa bảng tri thức.</summary>
    public string Schema { get; set; } = "kb";
}
