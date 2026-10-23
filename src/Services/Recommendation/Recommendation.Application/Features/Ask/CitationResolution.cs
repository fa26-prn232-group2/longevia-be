using Recommendation.Domain.Models;

namespace Recommendation.Application.Features.Ask;

/// <summary>
/// Kết quả kiểm chứng trích dẫn: chỉ giữ citation có <c>quote</c> khớp nguyên văn
/// với nội dung đoạn nguồn (sau khi gộp whitespace và bỏ dấu).
/// </summary>
public sealed record CitationResolution
{
    /// <summary>Các trích dẫn đã kiểm chứng.</summary>
    public required IReadOnlyList<Citation> Citations { get; init; }

    /// <summary>Không còn trích dẫn nào hợp lệ, câu trả lời phải hạ xuống abstain.</summary>
    public bool Abstained { get; init; }
}
