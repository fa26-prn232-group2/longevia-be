namespace Recommendation.Application.Abstractions;

/// <summary>
/// Kênh stream văn bản thuần (không JSON schema) cho chat hỏi sách. Tách khỏi
/// <see cref="IChatModel"/> để các use case structured hiện có không phải thay
/// đổi, và test double chỉ cần implement phần mình dùng.
/// </summary>
public interface IChatStreamModel
{
    /// <summary>
    /// Stream câu trả lời dạng text/markdown. Lỗi provider được ném ra ngoài để
    /// tầng gọi quyết định thông điệp cho người dùng.
    /// </summary>
    IAsyncEnumerable<string> StreamTextAsync(
        string systemInstruction,
        string userPrompt,
        CancellationToken cancellationToken);
}
