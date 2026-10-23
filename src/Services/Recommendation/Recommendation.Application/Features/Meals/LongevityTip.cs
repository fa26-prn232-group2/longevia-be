using Recommendation.Domain.Models;

namespace Recommendation.Application.Features.Meals;

/// <summary>
/// Mẹo trường thọ kèm trích dẫn. Khi kho tri thức không trả về đoạn phù hợp,
/// <see cref="Text"/> là tip tĩnh và <see cref="Citations"/> rỗng.
/// </summary>
public sealed record LongevityTip
{
    /// <summary>Nội dung mẹo hiển thị cho người dùng.</summary>
    public required string Text { get; init; }

    /// <summary>Trích dẫn chương/trang sách chống đỡ mẹo này.</summary>
    public IReadOnlyList<Citation> Citations { get; init; } = [];
}
