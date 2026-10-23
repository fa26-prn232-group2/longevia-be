using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Recommendation.Application.Abstractions;
using Recommendation.Domain.Models;
using Recommendation.Infrastructure.Adapters.Knowledge;
using Recommendation.IntegrationTests.Features.Meals;
using Recommendation.IntegrationTests.Features.Shared;

namespace Recommendation.IntegrationTests.Persistence;

/// <summary>
/// T114 - D-14: truy xuất lai phải <b>projection</b> và không mang cột
/// <c>embedding</c> về tầng ứng dụng. Kiểm tra cả hành vi (<c>Embedding</c> rỗng)
/// lẫn danh sách SELECT của câu SQL RRF.
/// </summary>
public sealed class ProjectionExcludesEmbeddingTests(RecommendationApiFixture fixture)
    : IClassFixture<RecommendationApiFixture>, IAsyncLifetime
{
    private const string Query = "chất béo không bão hòa trong dầu ô liu";

    private static readonly SeedChunk[] SeedChunks =
    [
        new()
        {
            Content =
                "Chất béo không bão hòa đơn có nhiều trong dầu ô liu và các loại hạt; "
                + "chất béo không bão hòa đa có trong cá và dầu thực vật.",
            BookPageFrom = 71,
            PdfPageFrom = 63,
            Verified = true,
        },
        new()
        {
            Content =
                "Chất béo là nguồn năng lượng dự trữ chính của cơ thể và tham gia tạo màng tế bào.",
            BookPageFrom = 72,
            PdfPageFrom = 64,
            Verified = true,
        },
    ];

    public Task InitializeAsync() =>
        CorpusSeeder.SeedAsync(fixture.Services, SeedChunks, CancellationToken.None);

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task HybridSearchReturnsChunksWithoutEmbeddingColumn()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IKnowledgeStore>();

        var results = await store.SearchAsync(
            new KnowledgeQuery { Query = Query },
            CancellationToken.None);

        Assert.NotEmpty(results);
        Assert.All(results, result => Assert.Empty(result.Chunk.Embedding));
    }

    [Fact]
    public void SearchSqlProjectionDoesNotSelectEmbedding()
    {
        var field = typeof(PgKnowledgeStore).GetField(
            "SearchSql",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(field);

        var sql = Assert.IsType<string>(field.GetValue(null));

        // Câu SQL có hai nhánh con dùng `c.embedding` để xếp hạng; chỉ danh sách
        // SELECT cuối cùng (projection trả về tầng ứng dụng) mới phải sạch vector.
        const string projectionStart = "SELECT c.id";
        const string projectionEnd = "FROM (SELECT";

        var start = sql.LastIndexOf(projectionStart, StringComparison.Ordinal);
        Assert.True(start >= 0, "Không tìm thấy danh sách SELECT cuối của câu SQL RRF.");

        var end = sql.IndexOf(projectionEnd, start, StringComparison.Ordinal);
        Assert.True(end > start, "Không tìm thấy mệnh đề FROM của projection.");

        var projection = sql[start..end];
        Assert.DoesNotContain("embedding", projection, StringComparison.OrdinalIgnoreCase);

        Assert.Null(typeof(ChunkSearchResult).GetProperty("Embedding"));
    }
}
