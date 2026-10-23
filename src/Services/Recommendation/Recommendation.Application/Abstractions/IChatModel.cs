using Recommendation.Domain.Models;

namespace Recommendation.Application.Abstractions;

/// <summary>
/// Cổng gọi mô hình ngôn ngữ để sinh phản hồi có cấu trúc. Lỗi provider hoặc
/// payload không đọc được được biểu diễn bằng kết quả abstain, không ném ra ngoài.
/// </summary>
public interface IChatModel
{
    /// <summary>Gửi prompt và nhận JSON đã parse theo schema của <see cref="StructuredPrompt.ResponseKind"/>.</summary>
    Task<ChatCompletion> CompleteAsync(StructuredPrompt prompt, CancellationToken cancellationToken);
}
