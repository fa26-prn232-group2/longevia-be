using System.Text.Json.Nodes;

namespace Recommendation.Domain.Models;

/// <summary>
/// Kết quả một lời gọi mô hình ngôn ngữ. Khi provider lỗi hoặc trả JSON không
/// đọc được, adapter trả tín hiệu abstain thay vì ném exception ra ngoài.
/// </summary>
public sealed record ChatCompletion
{
    /// <summary>Payload JSON đã parse; <c>null</c> khi <see cref="Abstained"/> là <c>true</c>.</summary>
    public JsonObject? Payload { get; init; }

    /// <summary>Provider không trả được phản hồi dùng được.</summary>
    public bool Abstained { get; init; }

    /// <summary>Lý do abstain ổn định để ghi log/telemetry, không hiển thị cho người dùng.</summary>
    public string? AbstainReason { get; init; }
}
