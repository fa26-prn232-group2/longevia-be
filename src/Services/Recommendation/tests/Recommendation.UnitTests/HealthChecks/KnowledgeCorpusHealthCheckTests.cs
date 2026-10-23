using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging.Abstractions;
using Recommendation.Api.HealthChecks;
using Recommendation.Application.Abstractions;
using Recommendation.Domain.Models;

namespace Recommendation.UnitTests.HealthChecks;

public sealed class KnowledgeCorpusHealthCheckTests
{
    [Fact]
    public async Task ReportsDegradedWhenTheCorpusHasNoChunks()
    {
        var check = new KnowledgeCorpusHealthCheck(
            new StubKnowledgeStore(0),
            NullLogger<KnowledgeCorpusHealthCheck>.Instance);

        var result = await check.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        Assert.Equal(HealthStatus.Degraded, result.Status);
        Assert.Contains("kb-init", result.Description, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReportsHealthyWhenTheCorpusIsLoaded()
    {
        var check = new KnowledgeCorpusHealthCheck(
            new StubKnowledgeStore(188),
            NullLogger<KnowledgeCorpusHealthCheck>.Instance);

        var result = await check.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.Contains("188", result.Description, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReportsUnhealthyWhenTheStoreCannotBeRead()
    {
        var check = new KnowledgeCorpusHealthCheck(
            new StubKnowledgeStore(new InvalidOperationException("db down")),
            NullLogger<KnowledgeCorpusHealthCheck>.Instance);

        var result = await check.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
    }

    private sealed class StubKnowledgeStore : IKnowledgeStore
    {
        private readonly int _chunks;
        private readonly Exception? _failure;

        public StubKnowledgeStore(int chunks) => _chunks = chunks;

        public StubKnowledgeStore(Exception failure) => _failure = failure;

        public Task<IReadOnlyList<RetrievedChunk>> SearchAsync(
            KnowledgeQuery query,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<RetrievedChunk>>([]);

        public Task<int> UpsertAsync(
            IReadOnlyList<KnowledgeChunk> chunks,
            CancellationToken cancellationToken) => Task.FromResult(0);

        public Task<int> CountAsync(string corpusKey, CancellationToken cancellationToken) =>
            Task.FromResult(_chunks);

        public Task<int> CountActiveAsync(CancellationToken cancellationToken) =>
            _failure is not null ? Task.FromException<int>(_failure) : Task.FromResult(_chunks);
    }
}
