namespace Recommendation.Infrastructure.Options;

/// <summary>
/// Cấu hình provider Google AI (Gemini Developer API).
/// Bind từ section <c>GOOGLE_AI</c> (env: <c>GOOGLE_AI__*</c>).
/// Model nhúng vector không nằm ở đây: nguồn duy nhất là
/// <see cref="RagOptions.EmbeddingModel"/> (env: <c>RAG__EMBEDDING_MODEL</c>).
/// </summary>
public sealed class GoogleAiOptions
{
    /// <summary>
    /// Tên section khớp tiền tố biến môi trường: <c>GOOGLE_AI__API_KEY</c> ->
    /// section <c>GOOGLE_AI</c>.
    /// </summary>
    public const string SectionName = "GOOGLE_AI";

    /// <summary>API key của Gemini Developer API. Không ghi log giá trị này.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Model sinh câu trả lời có căn cứ.</summary>
    public string ChatModel { get; set; } = "gemini-3.5-flash-lite";

    /// <summary>Timeout cho một lời gọi provider (milliseconds).</summary>
    public int TimeoutMs { get; set; } = 30_000;
}
