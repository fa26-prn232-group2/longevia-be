using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;
using Pgvector;
using Recommendation.Application.Abstractions;
using Recommendation.Application.Features.Search;
using Recommendation.Domain.Models;
using Recommendation.Infrastructure.Options;
using Recommendation.Infrastructure.Persistence;
using Recommendation.Infrastructure.Persistence.Entities;

namespace Recommendation.Infrastructure.Adapters.Knowledge;

/// <summary>
/// Hiện thực <see cref="IKnowledgeStore"/> trên PostgreSQL + pgvector: hai nhánh
/// truy xuất (vector cosine top-50, keyword tsvector top-50) merge bằng RRF
/// <c>SUM(1.0 / (60 + rank))</c> rồi lấy top-k (D-05, D-14).
/// </summary>
public sealed class PgKnowledgeStore : IKnowledgeStore
{
    private const int MaxTopK = 20;
    private const int RrfConstant = 60;

    private static readonly string SearchSql = $"""
        WITH vector_candidates AS (
            SELECT c.id,
                   ROW_NUMBER() OVER (ORDER BY c.embedding <=> {VectorQueryBuilder.CastExpression("@vector")}) AS rank
            FROM kb.chunk AS c
            WHERE c.corpus_id = @corpusId
              AND (@chapter = 0 OR c.chapter_number = @chapter)
              AND (NOT @requireVerified OR c.verified)
            ORDER BY c.embedding <=> {VectorQueryBuilder.CastExpression("@vector")}
            LIMIT @maxCandidates
        ), keyword_candidates AS (
            SELECT c.id,
                   ROW_NUMBER() OVER (
                       ORDER BY ts_rank(c.content_tsv, plainto_tsquery('simple', unaccent(@query))) DESC) AS rank
            FROM kb.chunk AS c
            WHERE c.corpus_id = @corpusId
              AND (@chapter = 0 OR c.chapter_number = @chapter)
              AND (NOT @requireVerified OR c.verified)
              AND c.content_tsv @@ plainto_tsquery('simple', unaccent(@query))
            LIMIT @maxCandidates
        )
        SELECT c.id            AS "Id",
               c.corpus_id     AS "CorpusId",
               c.content       AS "Content",
               c.chapter_number AS "ChapterNumber",
               ch.title        AS "ChapterTitle",
               c.book_page_from AS "BookPageFrom",
               c.book_page_to   AS "BookPageTo",
               c.pdf_page_from  AS "PdfPageFrom",
               c.pdf_page_to    AS "PdfPageTo",
               c.verified       AS "Verified",
               c.needs_review   AS "NeedsReview",
               ch.source_file   AS "SourceFile",
               SUM(1.0 / ({RrfConstant} + merged.rank)) AS "RrfScore"
        FROM (SELECT * FROM vector_candidates UNION ALL SELECT * FROM keyword_candidates) AS merged
        JOIN kb.chunk AS c ON c.id = merged.id
        JOIN kb.chapter AS ch ON ch.id = c.chapter_id
        GROUP BY c.id, ch.id
        ORDER BY "RrfScore" DESC
        LIMIT @topK;
        """;

    private readonly KnowledgeDbContext _db;
    private readonly IEmbeddingProvider _embeddingProvider;
    private readonly RagOptions _ragOptions;
    private readonly KnowledgeDbOptions _knowledgeDbOptions;

    public PgKnowledgeStore(
        KnowledgeDbContext db,
        IEmbeddingProvider embeddingProvider,
        IOptions<RagOptions> ragOptions,
        IOptions<KnowledgeDbOptions> knowledgeDbOptions)
    {
        ArgumentNullException.ThrowIfNull(ragOptions);
        ArgumentNullException.ThrowIfNull(knowledgeDbOptions);

        _db = db;
        _embeddingProvider = embeddingProvider;
        _ragOptions = ragOptions.Value;
        _knowledgeDbOptions = knowledgeDbOptions.Value;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<RetrievedChunk>> SearchAsync(
        KnowledgeQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var normalizedQuery = QueryNormalizer.Normalize(query.Query);
        if (normalizedQuery.Length == 0)
        {
            return [];
        }

        var corpusId = await ResolveActiveCorpusIdAsync(cancellationToken).ConfigureAwait(false);
        if (corpusId is null)
        {
            return [];
        }

        var embedding = await _embeddingProvider
            .EmbedAsync(normalizedQuery, cancellationToken)
            .ConfigureAwait(false);

        var rows = await SearchRowsAsync(query, normalizedQuery, embedding, corpusId.Value, cancellationToken)
            .ConfigureAwait(false);

        return rows.Select(MapToRetrievedChunk).ToList();
    }

    /// <inheritdoc />
    public async Task<int> UpsertAsync(
        IReadOnlyList<KnowledgeChunk> chunks,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(chunks);

        var written = 0;
        foreach (var chunk in chunks)
        {
            var embedding = ToVector(chunk);
            var existing = await _db.Chunk
                .FirstOrDefaultAsync(
                    entity => entity.CorpusId == chunk.CorpusId
                        && entity.ChapterId == chunk.ChapterId
                        && entity.ChunkIndex == chunk.ChunkIndex,
                    cancellationToken)
                .ConfigureAwait(false);

            if (existing is null)
            {
                _db.Chunk.Add(ToEntity(chunk, embedding));
            }
            else
            {
                UpdateEntity(existing, chunk, embedding);
            }

            written++;
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return written;
    }

    /// <inheritdoc />
    public Task<int> CountAsync(string corpusKey, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(corpusKey);

        return _db.Chunk
            .AsNoTracking()
            .CountAsync(
                entity => entity.Corpus != null
                    && entity.Corpus.CorpusKey == corpusKey
                    && entity.Corpus.IsActive,
                cancellationToken);
    }

    /// <inheritdoc />
    public Task<int> CountActiveAsync(CancellationToken cancellationToken) =>
        CountAsync(_knowledgeDbOptions.CorpusKey, cancellationToken);

    private Task<Guid?> ResolveActiveCorpusIdAsync(CancellationToken cancellationToken) =>
        _db.Corpus
            .AsNoTracking()
            .Where(corpus => corpus.CorpusKey == _knowledgeDbOptions.CorpusKey && corpus.IsActive)
            .Select(corpus => (Guid?)corpus.Id)
            .FirstOrDefaultAsync(cancellationToken);

    private async Task<IReadOnlyList<ChunkSearchResult>> SearchRowsAsync(
        KnowledgeQuery query,
        string normalizedQuery,
        float[] embedding,
        Guid corpusId,
        CancellationToken cancellationToken)
    {
        object[] parameters =
        [
            new NpgsqlParameter("vector", VectorQueryBuilder.ToSqlLiteral(embedding)),
            new NpgsqlParameter("corpusId", corpusId),
            new NpgsqlParameter("chapter", query.ChapterNumber),
            new NpgsqlParameter("query", normalizedQuery),
            new NpgsqlParameter("requireVerified", query.RequireVerified || _ragOptions.RequireVerified),
            new NpgsqlParameter("maxCandidates", _ragOptions.MaxCandidates),
            new NpgsqlParameter("topK", ResolveTopK(query.TopK)),
        ];

        return await _db.Set<ChunkSearchResult>()
            .FromSqlRaw(SearchSql, parameters)
            .AsNoTracking()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private int ResolveTopK(int requested) => requested switch
    {
        <= 0 => _ragOptions.TopK,
        > MaxTopK => MaxTopK,
        _ => requested,
    };

    private static RetrievedChunk MapToRetrievedChunk(ChunkSearchResult row) => new()
    {
        Chunk = new KnowledgeChunk
        {
            CorpusId = row.CorpusId,
            ChapterNumber = row.ChapterNumber,
            Content = row.Content,
            BookPageFrom = row.BookPageFrom,
            BookPageTo = row.BookPageTo,
            PdfPageFrom = row.PdfPageFrom,
            PdfPageTo = row.PdfPageTo,
            Verified = row.Verified,
            NeedsReview = row.NeedsReview,
            SourceFile = row.SourceFile,
            ChapterTitle = row.ChapterTitle,
        },
        Score = row.RrfScore,
    };

    private static Vector ToVector(KnowledgeChunk chunk)
    {
        if (chunk.Embedding.Count == 0)
        {
            throw new ArgumentException("Đoạn cần ghi phải có embedding.", nameof(chunk));
        }

        return new Vector(chunk.Embedding.ToArray());
    }

    private static Chunk ToEntity(KnowledgeChunk chunk, Vector embedding) => new()
    {
        CorpusId = chunk.CorpusId,
        ChapterId = chunk.ChapterId,
        ChapterNumber = chunk.ChapterNumber,
        ChunkIndex = chunk.ChunkIndex,
        Content = chunk.Content,
        TokenCount = chunk.TokenCount,
        ContentHash = chunk.ContentHash,
        BookPageFrom = chunk.BookPageFrom,
        BookPageTo = chunk.BookPageTo,
        PdfPageFrom = chunk.PdfPageFrom,
        PdfPageTo = chunk.PdfPageTo,
        Verified = chunk.Verified,
        NeedsReview = chunk.NeedsReview,
        Embedding = embedding,
    };

    private static void UpdateEntity(Chunk entity, KnowledgeChunk chunk, Vector embedding)
    {
        entity.ChapterNumber = chunk.ChapterNumber;
        entity.Content = chunk.Content;
        entity.TokenCount = chunk.TokenCount;
        entity.ContentHash = chunk.ContentHash;
        entity.BookPageFrom = chunk.BookPageFrom;
        entity.BookPageTo = chunk.BookPageTo;
        entity.PdfPageFrom = chunk.PdfPageFrom;
        entity.PdfPageTo = chunk.PdfPageTo;
        entity.Verified = chunk.Verified;
        entity.NeedsReview = chunk.NeedsReview;
        entity.Embedding = embedding;
    }
}
