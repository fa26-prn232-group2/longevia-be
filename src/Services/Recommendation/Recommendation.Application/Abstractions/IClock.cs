namespace Recommendation.Application.Abstractions;

/// <summary>
/// Đồng hồ hệ thống, tách ra để use case kiểm thử được thời gian.
/// </summary>
public interface IClock
{
    /// <summary>Thời điểm hiện tại theo UTC.</summary>
    DateTimeOffset UtcNow { get; }
}
