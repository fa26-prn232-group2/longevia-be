using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Recommendation.Application.Abstractions;
using Recommendation.Domain.Models;
using Recommendation.Infrastructure.Adapters.Knowledge;
using Recommendation.Infrastructure.Options;
using Recommendation.Infrastructure.Persistence;
using Recommendation.Infrastructure.Persistence.Entities;
using Testcontainers.PostgreSql;

namespace Recommendation.IntegrationTests.Persistence;

/// <summary>
/// T067 — nạp cùng một version hai lần vào PostgreSQL thật (Testcontainers) thì
/// số dòng <c>kb.chunk</c> không đổi và unique index
/// <c>(corpus_id, chapter_id, chunk_index)</c> vẫn được tôn trọng.
/// </summary>
public sealed class KnowledgeLoadTests : IAsyncLifetime
{
    private const int EmbeddingDimensions = 768;
    private const string CorpusKey = "che-do-an-truong-tho";

    private PostgreSqlContainer? _postgres;

    public async Task InitializeAsync()
    {
        _postgres = new PostgreSqlBuilder("pgvector/pgvector:pg17").Build();
        await _postgres.StartAsync();
    }

    public async Task DisposeAsync()
    {
        if (_postgres is not null)
        {
            await _postgres.DisposeAsync();
        }
    }

    [Fact]
    public async Task LoadingSameVersionTwiceKeepsChunkCount()
    {
        var connectionString = _postgres!.GetConnectionString();
        var knowledgeOptions = Options.Create(new KnowledgeDbOptions
        {
            ConnectionString = connectionString,
            CorpusKey = CorpusKey,
            Schema = "kb",
        });
        var dbOptions = new DbContextOptionsBuilder<KnowledgeDbContext>()
            .UseNpgsql(connectionString, npgsql => npgsql.UseVector())
            .Options;

        await using var db = new KnowledgeDbContext(dbOptions, knowledgeOptions);
        await db.Database.MigrateAsync();

        var (corpus, chapter) = await SeedCorpusAsync(db);
        var store = new PgKnowledgeStore(
            db,
            new StubEmbeddingProvider(),
            Options.Create(new RagOptions()),
            knowledgeOptions);

        var chunks = BuildChunks(corpus.Id, chapter.Id);

        var firstWrite = await store.UpsertAsync(chunks, CancellationToken.None);
        var afterFirst = await db.Chunk.AsNoTracking().CountAsync();

        var secondWrite = await store.UpsertAsync(chunks, CancellationToken.None);
        var afterSecond = await db.Chunk.AsNoTracking().CountAsync();

        Assert.Equal(3, firstWrite);
        Assert.Equal(3, secondWrite);
        Assert.Equal(3, afterFirst);
        Assert.Equal(3, afterSecond);

        var keys = await db.Chunk
            .AsNoTracking()
            .Select(chunk => new { chunk.CorpusId, chunk.ChapterId, chunk.ChunkIndex })
            .ToListAsync();
        Assert.Equal(3, keys.Distinct().Count());
    }

    [Fact]
    public async Task LoadingSameVersionUpdatesContentWithoutAddingRows()
    {
        var connectionString = _postgres!.GetConnectionString();
        var knowledgeOptions = Options.Create(new KnowledgeDbOptions
        {
            ConnectionString = connectionString,
            CorpusKey = CorpusKey,
            Schema = "kb",
        });
        var dbOptions = new DbContextOptionsBuilder<KnowledgeDbContext>()
            .UseNpgsql(connectionString, npgsql => npgsql.UseVector())
            .Options;

        await using var db = new KnowledgeDbContext(dbOptions, knowledgeOptions);
        await db.Database.MigrateAsync();

        var (corpus, chapter) = await SeedCorpusAsync(db);
        var store = new PgKnowledgeStore(
            db,
            new StubEmbeddingProvider(),
            Options.Create(new RagOptions()),
            knowledgeOptions);

        var chunks = BuildChunks(corpus.Id, chapter.Id);
        await store.UpsertAsync(chunks, CancellationToken.None);

        const string updatedContent = "Nội dung đã cập nhật cho lần nạp thứ hai.";
        var updated = chunks
            .Select(chunk => chunk.ChunkIndex == 1
                ? chunk with
                {
                    Content = updatedContent,
                    ContentHash = Hash(updatedContent),
                }
                : chunk)
            .ToList();

        await store.UpsertAsync(updated, CancellationToken.None);

        Assert.Equal(3, await db.Chunk.AsNoTracking().CountAsync());
        var stored = await db.Chunk.AsNoTracking().SingleAsync(chunk => chunk.ChunkIndex == 1);
        Assert.Equal(updatedContent, stored.Content);
        Assert.Equal(Hash(updatedContent), stored.ContentHash);
    }

    private static async Task<(Corpus Corpus, Chapter Chapter)> SeedCorpusAsync(KnowledgeDbContext db)
    {
        var corpus = new Corpus
        {
            Id = Guid.NewGuid(),
            CorpusKey = CorpusKey,
            Version = "test-version",
            Title = "Chế độ ăn trường thọ",
            SourcePath = "docs/reference/che-do-an-truong-tho",
            ChapterCount = 1,
            ChunkCount = 3,
            EmbeddingModel = "gemini-embedding-001",
            EmbeddingDim = EmbeddingDimensions,
            ChunkerParams = "{\"tokens\":1000,\"overlap\":150}",
            IsActive = true,
            EmbeddedAt = DateTimeOffset.UtcNow,
        };

        var chapter = new Chapter
        {
            Id = Guid.NewGuid(),
            CorpusId = corpus.Id,
            ChapterNumber = 1,
            Title = "Chương thử nghiệm",
            SourceFile = "01-chuong-1.md",
            BookPageFrom = 22,
            BookPageTo = 35,
            PdfPageFrom = 14,
            PdfPageTo = 27,
        };

        db.Corpus.Add(corpus);
        db.Chapter.Add(chapter);
        await db.SaveChangesAsync();
        return (corpus, chapter);
    }

    private static List<KnowledgeChunk> BuildChunks(Guid corpusId, Guid chapterId)
    {
        var pages = new[] { (22, 23, 14, 15), (24, 25, 16, 17), (26, 27, 18, 19) };
        var chunks = new List<KnowledgeChunk>(pages.Length);

        for (var index = 0; index < pages.Length; index++)
        {
            var (bookFrom, bookTo, pdfFrom, pdfTo) = pages[index];
            var content = $"Đoạn thử nghiệm số {index}.";
            chunks.Add(new KnowledgeChunk
            {
                CorpusId = corpusId,
                ChapterId = chapterId,
                ChapterNumber = 1,
                ChunkIndex = index,
                Content = content,
                TokenCount = 5,
                ContentHash = Hash(content),
                BookPageFrom = bookFrom,
                BookPageTo = bookTo,
                PdfPageFrom = pdfFrom,
                PdfPageTo = pdfTo,
                Verified = true,
                NeedsReview = false,
                Embedding = BuildVector(index),
            });
        }

        return chunks;
    }

    private static float[] BuildVector(int index)
    {
        var vector = new float[EmbeddingDimensions];
        vector[index] = 1f;
        return vector;
    }

    private static string Hash(string content) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content))).ToLowerInvariant();

    private sealed class StubEmbeddingProvider : IEmbeddingProvider
    {
        public Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken) =>
            Task.FromResult(new float[EmbeddingDimensions]);

        public Task<IReadOnlyList<float[]>> EmbedBatchAsync(
            IReadOnlyList<string> texts,
            CancellationToken cancellationToken)
        {
            IReadOnlyList<float[]> vectors = texts
                .Select(_ => new float[EmbeddingDimensions])
                .ToList();
            return Task.FromResult(vectors);
        }
    }
}
