namespace Recommendation.Domain.Models;

/// <summary>
/// Loại phản hồi có cấu trúc mà <c>IChatModel</c> phải sinh theo JSON schema
/// tương ứng. Schema cụ thể do tầng Infrastructure dựng.
/// </summary>
public enum StructuredResponseKind
{
    /// <summary>Câu trả lời có căn cứ: nội dung trả lời + danh sách trích dẫn.</summary>
    GroundedAnswer = 0,
}
