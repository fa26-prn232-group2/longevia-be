using Microsoft.Extensions.Options;
using Recommendation.Infrastructure.Options;

namespace Recommendation.Api.Deadlines;

/// <summary>
/// Chọn deadline phía server theo tên method gRPC (D-02/FR-054): hai RPC plan
/// dùng <see cref="RecommendationOptions.PlanDeadlineSeconds"/>, mọi RPC còn lại
/// dùng <see cref="RecommendationOptions.DeadlineSeconds"/>.
/// </summary>
/// <remarks>
/// Thuần, không I/O để unit test được mà không cần dựng gRPC server. Tên method
/// do <c>ServerCallContext.Method</c> trả về có dạng
/// <c>/longevia.recommendation.v1.RecommendationService/&lt;Method&gt;</c>.
/// </remarks>
public sealed class RpcDeadlinePolicy(IOptions<RecommendationOptions> options)
{
    private const string ServiceMethodPrefix =
        "/longevia.recommendation.v1.RecommendationService/";

    /// <summary>Tên đầy đủ của RPC sinh kế hoạch ăn uống.</summary>
    public const string GenerateMealPlanMethod = ServiceMethodPrefix + "GenerateMealPlan";

    /// <summary>Tên đầy đủ của RPC sinh kế hoạch tập luyện.</summary>
    public const string GenerateWorkoutPlanMethod = ServiceMethodPrefix + "GenerateWorkoutPlan";

    /// <summary>Deadline áp cho method gRPC tương ứng.</summary>
    public TimeSpan Resolve(string method)
    {
        ArgumentNullException.ThrowIfNull(method);

        var seconds = IsPlanMethod(method)
            ? options.Value.PlanDeadlineSeconds
            : options.Value.DeadlineSeconds;

        return TimeSpan.FromSeconds(seconds);
    }

    /// <summary><c>true</c> khi method là một trong hai RPC plan.</summary>
    public static bool IsPlanMethod(string? method) =>
        string.Equals(method, GenerateMealPlanMethod, StringComparison.Ordinal)
        || string.Equals(method, GenerateWorkoutPlanMethod, StringComparison.Ordinal);
}
