using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Recommendation.Application.Abstractions;
using Recommendation.Domain.Models;
using Recommendation.Infrastructure.Persistence;
using Recommendation.Infrastructure.Persistence.Entities;

namespace Recommendation.IntegrationTests.Features.Shared;

/// <summary>
/// Nạp một tập đoạn corpus thật vào <c>recommendation_db</c> của host test qua
/// <see cref="IKnowledgeStore.UpsertAsync"/> (I-06). Dùng chung cho mọi integration
/// test cần corpus, không copy-paste phần dựng entity.
/// </summary>
internal static class CorpusSeeder
{
    internal const string CorpusKey = "che-do-an-truong-tho";
    internal const int ChapterNumber = 4;
    internal const string ChapterTitle = "CHẾ ĐỘ ĂN TRƯỜNG THỌ";
    internal const string SourceFile = "04-chuong-4-che-do-an-truong-tho.md";

    private const int EmbeddingDimensions = 768;

    internal static async Task SeedAsync(
        IServiceProvider services,
        IReadOnlyList<SeedChunk> chunks,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(chunks);

        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<KnowledgeDbContext>();

        // xUnit dựng lại test class cho từng test method nên InitializeAsync chạy
        // nhiều lần trên cùng fixture: nạp lần đầu là đủ, không tạo corpus active thứ hai.
        var alreadySeeded = await db.Corpus
            .AsNoTracking()
            .AnyAsync(corpus => corpus.CorpusKey == CorpusKey && corpus.IsActive, cancellationToken)
            .ConfigureAwait(false);
        if (alreadySeeded)
        {
            return;
        }

        var corpus = new Corpus
        {
            Id = Guid.NewGuid(),
            CorpusKey = CorpusKey,
            Version = "integration-test",
            Title = "Chế độ ăn trường thọ",
            SourcePath = "docs/reference/che-do-an-truong-tho",
            ChapterCount = 1,
            ChunkCount = chunks.Count,
            EmbeddingModel = "gemini-embedding-001",
            EmbeddingDim = EmbeddingDimensions,
            ChunkerParams = "{\"tokens\":1000,\"overlap\":150}",
            IsActive = true,
            EmbeddedAt = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        var chapter = new Chapter
        {
            Id = Guid.NewGuid(),
            CorpusId = corpus.Id,
            ChapterNumber = ChapterNumber,
            Title = ChapterTitle,
            SourceFile = SourceFile,
            BookPageFrom = 69,
            BookPageTo = 105,
            PdfPageFrom = 61,
            PdfPageTo = 98,
        };

        db.Corpus.Add(corpus);
        db.Chapter.Add(chapter);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var store = scope.ServiceProvider.GetRequiredService<IKnowledgeStore>();
        await store.UpsertAsync(BuildChunks(corpus.Id, chapter.Id, chunks), cancellationToken)
            .ConfigureAwait(false);
    }

    private static List<KnowledgeChunk> BuildChunks(
        Guid corpusId,
        Guid chapterId,
        IReadOnlyList<SeedChunk> chunks)
    {
        var knowledgeChunks = new List<KnowledgeChunk>(chunks.Count);

        for (var index = 0; index < chunks.Count; index++)
        {
            var chunk = chunks[index];
            knowledgeChunks.Add(new KnowledgeChunk
            {
                CorpusId = corpusId,
                ChapterId = chapterId,
                ChapterNumber = ChapterNumber,
                ChunkIndex = index,
                Content = chunk.Content,
                TokenCount = chunk.Content.Length / 4,
                ContentHash = Hash(chunk.Content),
                BookPageFrom = chunk.BookPageFrom,
                BookPageTo = chunk.BookPageTo == 0 ? chunk.BookPageFrom : chunk.BookPageTo,
                PdfPageFrom = chunk.PdfPageFrom,
                PdfPageTo = chunk.PdfPageTo == 0 ? chunk.PdfPageFrom : chunk.PdfPageTo,
                Verified = chunk.Verified,
                NeedsReview = chunk.NeedsReview,
                Embedding = BuildVector(chunk.Content),
            });
        }

        return knowledgeChunks;
    }

    private static float[] BuildVector(string content)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        var vector = new float[EmbeddingDimensions];

        for (var index = 0; index < vector.Length; index++)
        {
            vector[index] = (hash[index % hash.Length] / 255f) - 0.5f;
        }

        var norm = MathF.Sqrt(vector.Sum(value => value * value));
        for (var index = 0; index < vector.Length; index++)
        {
            vector[index] /= norm;
        }

        return vector;
    }

    private static string Hash(string content) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content))).ToLowerInvariant();
}
