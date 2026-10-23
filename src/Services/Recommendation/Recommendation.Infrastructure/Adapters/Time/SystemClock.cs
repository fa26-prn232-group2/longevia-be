using Recommendation.Application.Abstractions;

namespace Recommendation.Infrastructure.Adapters.Time;

/// <summary>
/// Đồng hồ hệ thống dùng <see cref="DateTimeOffset.UtcNow"/>.
/// </summary>
public sealed class SystemClock : IClock
{
    /// <inheritdoc />
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
