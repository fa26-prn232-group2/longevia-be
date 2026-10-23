using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Recommendation.Application.Abstractions;
using Recommendation.Infrastructure.Adapters.Knowledge;
using Recommendation.Infrastructure.Options;
using Recommendation.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace Recommendation.IntegrationTests.Persistence;

/// <summary>
/// T088 — nạp seed .NET vào PostgreSQL thật (Testcontainers): ghi đúng
/// corpus/chapter/chunk/page_flag trong một transaction, idempotent, bỏ qua khi
/// <c>--skip-if-loaded</c> và fail khi seed hoặc database lệch metadata.
/// </summary>
public sealed class SeedLoaderTests : IAsyncLifetime
{
    private const int EmbeddingDimensions = 768;
    private const string CorpusKey = "che-do-an-truong-tho";
    private const string SeedVersion = "seed-test-version";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private readonly string _seedPath = Path.Combine(
        Path.GetTempPath(),
        "longevia-seed-" + Guid.NewGuid().ToString("N"));

    private PostgreSqlContainer? _postgres;

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(_seedPath);
        await WriteSeedAsync(_seedPath);

        _postgres = new PostgreSqlBuilder("pgvector/pgvector:pg17").Build();
        await _postgres.StartAsync();
    }

    public async Task DisposeAsync()
    {
        if (_postgres is not null)
        {
            await _postgres.DisposeAsync();
        }

        if (Directory.Exists(_seedPath))
        {
            Directory.Delete(_seedPath, recursive: true);
        }
    }

    [Fact]
    public async Task LoadingSeedIntoEmptyDatabaseWritesCorpus()
    {
        await using var db = await CreateContextAsync();
        var loader = CreateLoader(db);

        var result = await loader.LoadAsync(new SeedLoadOptions
        {
            SeedPath = _seedPath,
            Verify = true,
        });

        Assert.False(result.Skipped);
        Assert.Equal(CorpusKey, result.CorpusKey);
        Assert.Equal(SeedVersion, result.Version);
        Assert.Equal(1, result.Chapters);
        Assert.Equal(2, result.Chunks);
        Assert.Equal(1, result.PageFlags);
        Assert.NotNull(result.Verification);
        Assert.True(
            result.Verification!.Ok,
            string.Join(" | ", result.Verification.Failures));

        var corpus = await db.Corpus.AsNoTracking().SingleAsync();
        Assert.True(corpus.IsActive);
        Assert.Equal(1, corpus.ChapterCount);
        Assert.Equal(2, corpus.ChunkCount);
        Assert.Equal(EmbeddingDimensions, corpus.EmbeddingDim);
        Assert.Contains("tokens", corpus.ChunkerParams, StringComparison.Ordinal);

        var chapter = Assert.Single(await db.Chapter.AsNoTracking().ToListAsync());
        Assert.Equal(1, chapter.ChapterNumber);

        var chunks = await db.Chunk
            .AsNoTracking()
            .OrderBy(chunk => chunk.ChunkIndex)
            .ToListAsync();
        Assert.Equal(2, chunks.Count);
        Assert.All(chunks, chunk => Assert.Equal(EmbeddingDimensions, chunk.Embedding.ToArray().Length));
        Assert.All(chunks, chunk => Assert.NotNull(chunk.ContentTsv));

        var flag = Assert.Single(await db.PageFlag.AsNoTracking().ToListAsync());
        Assert.Equal(2, flag.PdfPage);
        Assert.Equal(11, flag.BookPage);
    }

    [Fact]
    public async Task SkipIfLoadedLeavesExistingRowsUntouched()
    {
        await using var db = await CreateContextAsync();
        var loader = CreateLoader(db);

        var first = await loader.LoadAsync(new SeedLoadOptions { SeedPath = _seedPath });

        await db.Database.ExecuteSqlRawAsync(
            "UPDATE kb.chunk SET content = 'mutated' WHERE chunk_index = 0;");

        var second = await loader.LoadAsync(new SeedLoadOptions
        {
            SeedPath = _seedPath,
            SkipIfLoaded = true,
        });

        Assert.False(first.Skipped);
        Assert.True(second.Skipped);
        Assert.Equal(first.CorpusId, second.CorpusId);
        Assert.Equal(2, await db.Chunk.AsNoTracking().CountAsync());
        Assert.Equal(
            "mutated",
            await db.Chunk.AsNoTracking()
                .Where(chunk => chunk.ChunkIndex == 0)
                .Select(chunk => chunk.Content)
                .SingleAsync());
    }

    [Fact]
    public async Task LoadingSameVersionTwiceDoesNotDuplicateRows()
    {
        await using var db = await CreateContextAsync();
        var loader = CreateLoader(db);

        await loader.LoadAsync(new SeedLoadOptions { SeedPath = _seedPath });
        await loader.LoadAsync(new SeedLoadOptions { SeedPath = _seedPath });

        Assert.Equal(1, await db.Corpus.CountAsync());
        Assert.Equal(1, await db.Chapter.CountAsync());
        Assert.Equal(2, await db.Chunk.CountAsync());
        Assert.Equal(1, await db.PageFlag.CountAsync());
    }

    [Fact]
    public async Task LoadingNewVersionDeactivatesPreviousOne()
    {
        var newerSeedPath = Path.Combine(_seedPath, "newer-version");
        await WriteSeedAsync(newerSeedPath, version: "seed-test-version-2");

        await using var db = await CreateContextAsync();
        var loader = CreateLoader(db);

        await loader.LoadAsync(new SeedLoadOptions { SeedPath = _seedPath });
        await loader.LoadAsync(new SeedLoadOptions { SeedPath = newerSeedPath });

        var corpora = await db.Corpus.AsNoTracking().ToListAsync();
        Assert.Equal(2, corpora.Count);
        var active = Assert.Single(corpora, corpus => corpus.IsActive);
        Assert.Equal("seed-test-version-2", active.Version);
    }

    [Fact]
    public async Task LoadingSeedWithMismatchedEmbeddingDimensionsFails()
    {
        var badSeedPath = Path.Combine(_seedPath, "bad-dimension");
        await WriteSeedAsync(badSeedPath, chunkEmbeddingDimensions: EmbeddingDimensions - 1);

        await using var db = await CreateContextAsync();
        var loader = CreateLoader(db);

        await Assert.ThrowsAsync<SeedLoadException>(
            () => loader.LoadAsync(new SeedLoadOptions { SeedPath = badSeedPath }));

        Assert.Equal(0, await db.Corpus.CountAsync());
    }

    [Fact]
    public async Task SeedWithDifferentCorpusKeyIsRejected()
    {
        await using var db = await CreateContextAsync();
        var loader = CreateLoader(db, expectedCorpusKey: "another-corpus");

        await Assert.ThrowsAsync<SeedLoadException>(
            () => loader.LoadAsync(new SeedLoadOptions { SeedPath = _seedPath }));

        Assert.Equal(0, await db.Corpus.CountAsync());
    }

    [Fact]
    public async Task VerifyDetectsEmbeddingDimensionDrift()
    {
        await using var db = await CreateContextAsync();
        var loader = CreateLoader(db);

        await loader.LoadAsync(new SeedLoadOptions { SeedPath = _seedPath });

        await db.Database.ExecuteSqlRawAsync("UPDATE kb.corpus SET embedding_dim = 512;");

        await Assert.ThrowsAsync<SeedLoadException>(
            () => loader.LoadAsync(new SeedLoadOptions
            {
                SeedPath = _seedPath,
                SkipIfLoaded = true,
                Verify = true,
            }));
    }

    private async Task<KnowledgeDbContext> CreateContextAsync()
    {
        var connectionString = _postgres!.GetConnectionString();
        var dbOptions = new DbContextOptionsBuilder<KnowledgeDbContext>()
            .UseNpgsql(connectionString, npgsql => npgsql.UseVector())
            .Options;

        var db = new KnowledgeDbContext(
            dbOptions,
            Options.Create(new KnowledgeDbOptions
            {
                ConnectionString = connectionString,
                CorpusKey = CorpusKey,
                Schema = "kb",
            }));

        await db.Database.MigrateAsync();
        await db.Database.ExecuteSqlRawAsync(
            "TRUNCATE TABLE kb.page_flag, kb.chunk, kb.chapter, kb.corpus RESTART IDENTITY CASCADE;");

        return db;
    }

    private SeedLoader CreateLoader(KnowledgeDbContext db, string expectedCorpusKey = CorpusKey) =>
        new(
            db,
            Options.Create(new KnowledgeDbOptions
            {
                ConnectionString = _postgres!.GetConnectionString(),
                CorpusKey = expectedCorpusKey,
                Schema = "kb",
            }),
            NullLogger<SeedLoader>.Instance);

    private static async Task WriteSeedAsync(
        string seedPath,
        int chunkEmbeddingDimensions = EmbeddingDimensions,
        string version = SeedVersion)
    {
        Directory.CreateDirectory(seedPath);

        const string firstContent = "Đoạn thử nghiệm thứ nhất về chế độ ăn trường thọ.";
        const string secondContent = "Đoạn thử nghiệm thứ hai về thói quen sinh hoạt.";

        var build = new
        {
            CorpusKey,
            Version = version,
            Title = "Chế độ ăn trường thọ (test)",
            SourcePath = "docs/reference/che-do-an-truong-tho",
            ChapterCount = 1,
            ChunkCount = 2,
            PageFlagCount = 1,
            EmbeddingModel = "gemini-embedding-001",
            EmbeddingDim = EmbeddingDimensions,
            ChunkerParams = new { Tokens = 1000, Overlap = 150 },
            EmbeddedAt = DateTimeOffset.UtcNow,
            Chapters = new[]
            {
                new
                {
                    Number = 1,
                    Title = "Chương thử nghiệm",
                    SourceFile = "01-chuong-1.md",
                    BookPageFrom = 10,
                    BookPageTo = 20,
                    PdfPageFrom = 1,
                    PdfPageTo = 11,
                },
            },
        };

        await File.WriteAllTextAsync(
            Path.Combine(seedPath, "build.json"),
            JsonSerializer.Serialize(build, JsonOptions),
            new UTF8Encoding(false));

        var chunks = new[]
        {
            new
            {
                ChapterNumber = 1,
                ChunkIndex = 0,
                Content = firstContent,
                TokenCount = 8,
                ContentHash = Hash(firstContent),
                BookPageFrom = 10,
                BookPageTo = 12,
                PdfPageFrom = 1,
                PdfPageTo = 3,
                Verified = true,
                NeedsReview = false,
                Embedding = BuildVector(chunkEmbeddingDimensions, 0),
            },
            new
            {
                ChapterNumber = 1,
                ChunkIndex = 1,
                Content = secondContent,
                TokenCount = 7,
                ContentHash = Hash(secondContent),
                BookPageFrom = 13,
                BookPageTo = 15,
                PdfPageFrom = 4,
                PdfPageTo = 6,
                Verified = false,
                NeedsReview = true,
                Embedding = BuildVector(chunkEmbeddingDimensions, 1),
            },
        };

        await WriteGzipJsonlAsync(Path.Combine(seedPath, "chunks.jsonl.gz"), chunks);

        var flags = new[]
        {
            new
            {
                PdfPage = 2,
                BookPage = 11,
                Kind = "missing_numbers",
                SourceFile = "review/qa.md",
                Note = "10",
            },
        };

        await WriteGzipJsonlAsync(Path.Combine(seedPath, "page_flags.jsonl.gz"), flags);
    }

    private static async Task WriteGzipJsonlAsync<T>(string path, IEnumerable<T> rows)
    {
        await using var file = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
        await using var gzip = new GZipStream(file, CompressionLevel.SmallestSize);
        await using var writer = new StreamWriter(gzip, new UTF8Encoding(false));

        foreach (var row in rows)
        {
            await writer.WriteLineAsync(JsonSerializer.Serialize(row, JsonOptions));
        }
    }

    private static float[] BuildVector(int dimensions, int index)
    {
        var vector = new float[dimensions];
        vector[index % dimensions] = 1f;
        return vector;
    }

    private static string Hash(string content) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content))).ToLowerInvariant();
}
