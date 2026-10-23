using Microsoft.Extensions.Logging;

namespace Recommendation.Api.Extensions;

/// <summary>
/// Thiết lập logging có cấu trúc cho Recommendation.
/// </summary>
public static class TelemetryExtensions
{
    /// <summary>
    /// Ghi log ở dạng JSON để máy thu thập log đọc được theo trường.
    /// Không bao giờ ghi secret, token hay dữ liệu sức khỏe (Constitution §I).
    /// </summary>
    public static IServiceCollection AddRecommendationTelemetry(this IServiceCollection services)
    {
        services.AddLogging(logging =>
        {
            logging.ClearProviders();
            logging.AddJsonConsole(options =>
            {
                options.IncludeScopes = true;
                options.UseUtcTimestamp = true;
                options.TimestampFormat = "yyyy-MM-dd'T'HH:mm:ss.fff'Z'";
            });
        });

        return services;
    }
}
