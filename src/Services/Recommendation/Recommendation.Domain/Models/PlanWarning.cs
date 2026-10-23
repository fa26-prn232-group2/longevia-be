namespace Recommendation.Domain.Models;

/// <summary>
/// Cảnh báo của hai RPC plan (data-model §4.5/§7): mã ổn định + mô tả tiếng Việt
/// ngắn gọn + phạm vi (<c>plan</c> hoặc ngày ISO). Không chứa nội dung kế hoạch.
/// </summary>
public sealed record PlanWarning
{
    /// <summary>Phạm vi cảnh báo áp cho toàn bộ kế hoạch.</summary>
    public const string PlanScope = "plan";

    /// <summary>Mã cảnh báo ổn định, xem <c>PlanWarningCodes</c>.</summary>
    public required string Code { get; init; }

    /// <summary>Mô tả tiếng Việt ngắn gọn để caller hiển thị hoặc ghi log.</summary>
    public required string Message { get; init; }

    /// <summary><c>plan</c> hoặc ngày ISO <c>YYYY-MM-DD</c> mà cảnh báo áp dụng.</summary>
    public string Scope { get; init; } = PlanScope;
}
