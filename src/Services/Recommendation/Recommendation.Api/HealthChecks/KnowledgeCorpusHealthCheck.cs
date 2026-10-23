using Microsoft.Extensions.Diagnostics.HealthChecks;
using Recommendation.Application.Abstractions;

namespace Recommendation.Api.HealthChecks;

/// <summary>
/// Health check của kho tri thức: phát hiện trường hợp service chạy nhưng corpus
/// chưa được nạp (môi trường mới, hoặc <c>kb-init</c> bị bỏ qua). Nếu không có
/// check này, mọi câu hỏi chỉ trả về abstain và trông như "sách không có nội dung".
/// </summary>
public sealed class KnowledgeCorpusHealthCheck(
    IKnowledgeStore knowledgeStore,
    ILogger<KnowledgeCorpusHealthCheck> logger) : IHealthCheck
{
    private const string EmptyDescription =
        "Knowledge corpus is empty - run 'docker compose up -d' (service kb-init) or 'make kb-ensure'.";

    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        int chunks;
        try
        {
            chunks = await knowledgeStore.CountActiveAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Knowledge corpus health check could not reach the store.");
            return HealthCheckResult.Unhealthy("Knowledge store is unreachable.", exception);
        }

        if (chunks == 0)
        {
            logger.LogWarning("Knowledge corpus is empty; grounded answers will abstain.");
            return HealthCheckResult.Degraded(EmptyDescription);
        }

        return HealthCheckResult.Healthy($"Knowledge corpus loaded: {chunks} chunks.");
    }
}
