using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pgvector;
using Recommendation.Application.Abstractions;
using Recommendation.Infrastructure.Options;
using Recommendation.Infrastructure.Persistence;
using Recommendation.Infrastructure.Persistence.Entities;

namespace Recommendation.Infrastructure.Adapters.Knowledge;

/// <summary>
/// Nạp seed tri thức đã xuất (<c>build.json</c>, <c>chunks.jsonl.gz</c>,
/// <c>page_flags.jsonl.gz</c>) vào <c>recommendation_db</c> theo D-15.
///
/// Mọi thao tác ghi nằm trong một transaction và idempotent theo khoá tự nhiên:
/// <c>kb.corpus (corpus_key, version)</c>, <c>kb.chapter (corpus_id, chapter_number)</c>,
/// <c>kb.chunk (corpus_id, chapter_id, chunk_index)</c>,
/// <c>kb.page_flag (corpus_id, pdf_page, kind, source_file)</c>.
/// Cột <c>content_tsv</c> không bao giờ được ghi vì trigger của migration tự tính.
/// </summary>
public sealed class SeedLoader : ISeedLoader
{
    private const int Sha256HexLength = 64;
    private const string BuildFileName = "build.json";
    private const string ChunksFileName = "chunks.jsonl";
    private const string PageFlagsFileName = "page_flags.jsonl";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    private readonly KnowledgeDbContext _db;
    private readonly KnowledgeDbOptions _knowledgeDbOptions;
    private readonly ILogger<SeedLoader> _logger;

    public SeedLoader(
        KnowledgeDbContext db,
        IOptions<KnowledgeDbOptions> knowledgeDbOptions,
        ILogger<SeedLoader> logger)
    {
        ArgumentNullException.ThrowIfNull(knowledgeDbOptions);
        ArgumentNullException.ThrowIfNull(logger);

        _db = db;
        _knowledgeDbOptions = knowledgeDbOptions.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<SeedLoadResult> LoadAsync(
        SeedLoadOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrWhiteSpace(options.SeedPath))
        {
            throw new SeedLoadException("Seed path không được để trống.");
        }

        var seedPath = Path.GetFullPath(options.SeedPath);
        var build = await ReadBuildAsync(seedPath, cancellationToken).ConfigureAwait(false);
        var chapters = ValidateBuild(build);
        var corpusKey = build.CorpusKey!;
        var version = build.Version!;

        if (options.SkipIfLoaded)
        {
            var loaded = await _db.Corpus
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    corpus => corpus.CorpusKey == corpusKey
                        && corpus.Version == version
                        && corpus.IsActive,
                    cancellationToken)
                .ConfigureAwait(false);

            if (loaded is not null)
            {
                var verification = options.Verify
                    ? await VerifyAsync(build, cancellationToken).ConfigureAwait(false)
                    : null;
                EnsureVerified(verification);

                _logger.LogInformation(
                    "Seed '{CorpusKey}' version {Version} is already active; skipping load.",
                    corpusKey,
                    version);

                return new SeedLoadResult
                {
                    Skipped = true,
                    Reason = $"Corpus '{corpusKey}' version {version} đã active.",
                    CorpusId = loaded.Id,
                    CorpusKey = corpusKey,
                    Version = version,
                    Chapters = verification?.Chapters ?? build.ChapterCount,
                    Chunks = verification?.Chunks ?? build.ChunkCount,
                    PageFlags = verification?.PageFlags ?? build.PageFlagCount ?? 0,
                    Verification = verification,
                };
            }
        }

        var chunks = await ReadJsonlAsync<SeedChunk>(seedPath, ChunksFileName, cancellationToken)
            .ConfigureAwait(false);
        var pageFlags = await ReadJsonlAsync<SeedPageFlag>(seedPath, PageFlagsFileName, cancellationToken)
            .ConfigureAwait(false);

        ValidateChunks(build, chapters, chunks);
        ValidatePageFlags(build, pageFlags);

        var corpusId = await WriteAsync(build, chapters, chunks, pageFlags, cancellationToken)
            .ConfigureAwait(false);

        var report = options.Verify
            ? await VerifyAsync(build, cancellationToken).ConfigureAwait(false)
            : null;
        EnsureVerified(report);

        _logger.LogInformation(
            "Seeded corpus '{CorpusKey}' version {Version}: {Chapters} chapters, {Chunks} chunks, {PageFlags} page flags.",
            corpusKey,
            version,
            chapters.Count,
            chunks.Count,
            pageFlags.Count);

        return new SeedLoadResult
        {
            Skipped = false,
            CorpusId = corpusId,
            CorpusKey = corpusKey,
            Version = version,
            Chapters = chapters.Count,
            Chunks = chunks.Count,
            PageFlags = pageFlags.Count,
            Verification = report,
        };
    }

    private static async Task<SeedBuild> ReadBuildAsync(string seedPath, CancellationToken cancellationToken)
    {
        var path = Path.Combine(seedPath, BuildFileName);
        if (!File.Exists(path))
        {
            throw new SeedLoadException($"Không tìm thấy '{BuildFileName}' trong '{seedPath}'.");
        }

        string json;
        try
        {
            json = await File.ReadAllTextAsync(path, Encoding.UTF8, cancellationToken).ConfigureAwait(false);
        }
        catch (IOException exception)
        {
            throw new SeedLoadException($"Không đọc được '{path}'.", exception);
        }

        SeedBuild? build;
        try
        {
            build = JsonSerializer.Deserialize<SeedBuild>(json, JsonOptions);
        }
        catch (JsonException exception)
        {
            throw new SeedLoadException($"'{BuildFileName}' không phải JSON hợp lệ.", exception);
        }

        return build ?? throw new SeedLoadException($"'{BuildFileName}' phải là một JSON object.");
    }

    private static async Task<List<T>> ReadJsonlAsync<T>(
        string seedPath,
        string fileName,
        CancellationToken cancellationToken)
    {
        var path = ResolveArtifact(seedPath, fileName);
        var rows = new List<T>();

        await using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        await using var decompressed = IsGzip(path)
            ? new GZipStream(file, CompressionMode.Decompress)
            : null;
        await using var stream = (Stream?)decompressed ?? file;
        using var reader = new StreamReader(stream, Encoding.UTF8);

        while (await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            try
            {
                var row = JsonSerializer.Deserialize<T>(line, JsonOptions);
                if (row is null)
                {
                    throw new SeedLoadException($"'{fileName}' có dòng rỗng không hợp lệ.");
                }

                rows.Add(row);
            }
            catch (JsonException exception)
            {
                throw new SeedLoadException($"'{fileName}' có dòng không phải JSON hợp lệ.", exception);
            }
        }

        return rows;
    }

    private static string ResolveArtifact(string seedPath, string fileName)
    {
        var candidate = Path.Combine(seedPath, fileName);
        if (File.Exists(candidate))
        {
            return candidate;
        }

        var compressed = candidate + ".gz";
        if (File.Exists(compressed))
        {
            return compressed;
        }

        throw new SeedLoadException($"Không tìm thấy '{fileName}' (hoặc '{fileName}.gz') trong '{seedPath}'.");
    }

    private static bool IsGzip(string path) =>
        path.EndsWith(".gz", StringComparison.OrdinalIgnoreCase);

    private IReadOnlyList<SeedChapter> ValidateBuild(SeedBuild build)
    {
        if (!string.Equals(build.CorpusKey, _knowledgeDbOptions.CorpusKey, StringComparison.Ordinal))
        {
            throw new SeedLoadException(
                $"corpus_key của seed ('{build.CorpusKey}') không khớp cấu hình "
                + $"KNOWLEDGE_DB__CORPUS_KEY ('{_knowledgeDbOptions.CorpusKey}').");
        }

        if (string.IsNullOrWhiteSpace(build.Version))
        {
            throw new SeedLoadException("build.json thiếu 'version'.");
        }

        if (build.EmbeddingDim is <= 0 or > 3072)
        {
            throw new SeedLoadException("build.json có 'embedding_dim' không hợp lệ.");
        }

        if (build.EmbeddedAt == default)
        {
            throw new SeedLoadException("build.json thiếu 'embedded_at'.");
        }

        if (build.Chapters is not { Count: > 0 } chapters)
        {
            throw new SeedLoadException("build.json thiếu danh sách chương.");
        }

        if (build.ChapterCount != chapters.Count)
        {
            throw new SeedLoadException(
                $"build.json ghi {build.ChapterCount} chương nhưng danh sách có {chapters.Count}.");
        }

        if (build.ChunkCount <= 0)
        {
            throw new SeedLoadException("build.json thiếu 'chunk_count' hợp lệ.");
        }

        var numbers = new HashSet<int>();
        foreach (var chapter in chapters)
        {
            if (!numbers.Add(chapter.Number))
            {
                throw new SeedLoadException($"build.json có chương trùng số {chapter.Number}.");
            }

            if (string.IsNullOrWhiteSpace(chapter.Title) || string.IsNullOrWhiteSpace(chapter.SourceFile))
            {
                throw new SeedLoadException($"Chương {chapter.Number} thiếu title hoặc source_file.");
            }

            if (chapter.BookPageFrom <= 0
                || chapter.BookPageTo < chapter.BookPageFrom
                || chapter.PdfPageFrom <= 0
                || chapter.PdfPageTo < chapter.PdfPageFrom)
            {
                throw new SeedLoadException($"Chương {chapter.Number} có khoảng trang không hợp lệ.");
            }
        }

        return chapters;
    }

    private static void ValidateChunks(
        SeedBuild build,
        IReadOnlyList<SeedChapter> chapters,
        IReadOnlyList<SeedChunk> chunks)
    {
        if (chunks.Count != build.ChunkCount)
        {
            throw new SeedLoadException(
                $"build.json ghi {build.ChunkCount} chunk nhưng artifact có {chunks.Count}.");
        }

        var chapterNumbers = chapters.Select(chapter => chapter.Number).ToHashSet();
        var seen = new HashSet<(int Chapter, int Index)>();

        foreach (var chunk in chunks)
        {
            if (!chapterNumbers.Contains(chunk.ChapterNumber))
            {
                throw new SeedLoadException(
                    $"Chunk thuộc chương không có trong build.json: {chunk.ChapterNumber}.");
            }

            if (!seen.Add((chunk.ChapterNumber, chunk.ChunkIndex)))
            {
                throw new SeedLoadException(
                    $"Chunk trùng khoá chương {chunk.ChapterNumber} / index {chunk.ChunkIndex}.");
            }

            if (string.IsNullOrWhiteSpace(chunk.Content))
            {
                throw new SeedLoadException(
                    $"Chunk {chunk.ChapterNumber}/{chunk.ChunkIndex} thiếu content.");
            }

            if (string.IsNullOrWhiteSpace(chunk.ContentHash))
            {
                throw new SeedLoadException(
                    $"Chunk {chunk.ChapterNumber}/{chunk.ChunkIndex} thiếu content_hash.");
            }

            if (chunk.Embedding is not { Length: > 0 } embedding
                || embedding.Length != build.EmbeddingDim)
            {
                throw new SeedLoadException(
                    $"Chunk {chunk.ChapterNumber}/{chunk.ChunkIndex} không có embedding "
                    + $"{build.EmbeddingDim} chiều.");
            }

            if (embedding.Any(value => !float.IsFinite(value)))
            {
                throw new SeedLoadException(
                    $"Chunk {chunk.ChapterNumber}/{chunk.ChunkIndex} có embedding không hữu hạn.");
            }

            if (chunk.TokenCount < 0
                || chunk.BookPageFrom <= 0
                || chunk.BookPageTo < chunk.BookPageFrom
                || chunk.PdfPageFrom <= 0
                || chunk.PdfPageTo < chunk.PdfPageFrom)
            {
                throw new SeedLoadException(
                    $"Chunk {chunk.ChapterNumber}/{chunk.ChunkIndex} có metadata không hợp lệ.");
            }
        }
    }

    private static void ValidatePageFlags(SeedBuild build, IReadOnlyList<SeedPageFlag> pageFlags)
    {
        if (build.PageFlagCount is int expected && expected != pageFlags.Count)
        {
            throw new SeedLoadException(
                $"build.json ghi {expected} page flag nhưng artifact có {pageFlags.Count}.");
        }

        var seen = new HashSet<(int PdfPage, string Kind, string SourceFile)>();
        foreach (var flag in pageFlags)
        {
            if (string.IsNullOrWhiteSpace(flag.Kind) || string.IsNullOrWhiteSpace(flag.SourceFile))
            {
                throw new SeedLoadException("Page flag thiếu kind hoặc source_file.");
            }

            if (flag.PdfPage <= 0 || flag.BookPage <= 0)
            {
                throw new SeedLoadException("Page flag có số trang không hợp lệ.");
            }

            if (!seen.Add((flag.PdfPage, flag.Kind, flag.SourceFile)))
            {
                throw new SeedLoadException(
                    $"Page flag trùng khoá ({flag.PdfPage}, {flag.Kind}, {flag.SourceFile}).");
            }
        }
    }

    private async Task<Guid> WriteAsync(
        SeedBuild build,
        IReadOnlyList<SeedChapter> chapters,
        IReadOnlyList<SeedChunk> chunks,
        IReadOnlyList<SeedPageFlag> pageFlags,
        CancellationToken cancellationToken)
    {
        var corpusKey = build.CorpusKey!;
        var version = build.Version!;

        await using var transaction = await _db.Database
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);

        // Chỉ một version active cho mỗi corpus_key (unique index ux_corpus_active).
        await _db.Corpus
            .Where(corpus => corpus.CorpusKey == corpusKey && corpus.IsActive && corpus.Version != version)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(corpus => corpus.IsActive, false),
                cancellationToken)
            .ConfigureAwait(false);

        var corpusEntity = await _db.Corpus
            .FirstOrDefaultAsync(
                corpus => corpus.CorpusKey == corpusKey && corpus.Version == version,
                cancellationToken)
            .ConfigureAwait(false);

        if (corpusEntity is null)
        {
            corpusEntity = new Corpus
            {
                Id = Guid.NewGuid(),
                CorpusKey = corpusKey,
                Version = version,
                CreatedAt = DateTimeOffset.UtcNow,
            };
            _db.Corpus.Add(corpusEntity);
        }

        corpusEntity.Title = build.Title ?? string.Empty;
        corpusEntity.SourcePath = build.SourcePath ?? string.Empty;
        corpusEntity.ChapterCount = chapters.Count;
        corpusEntity.ChunkCount = chunks.Count;
        corpusEntity.EmbeddingModel = build.EmbeddingModel ?? string.Empty;
        corpusEntity.EmbeddingDim = build.EmbeddingDim;
        corpusEntity.ChunkerParams = SerializeChunkerParams(build.ChunkerParams);
        corpusEntity.IsActive = true;
        corpusEntity.EmbeddedAt = build.EmbeddedAt;

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var existingChapters = await _db.Chapter
            .Where(chapter => chapter.CorpusId == corpusEntity.Id)
            .ToDictionaryAsync(chapter => chapter.ChapterNumber, cancellationToken)
            .ConfigureAwait(false);

        var chaptersByNumber = new Dictionary<int, Chapter>();
        foreach (var seedChapter in chapters)
        {
            if (!existingChapters.TryGetValue(seedChapter.Number, out var chapterEntity))
            {
                chapterEntity = new Chapter
                {
                    Id = Guid.NewGuid(),
                    CorpusId = corpusEntity.Id,
                    ChapterNumber = seedChapter.Number,
                };
                _db.Chapter.Add(chapterEntity);
            }

            chapterEntity.Title = seedChapter.Title!;
            chapterEntity.SourceFile = seedChapter.SourceFile!;
            chapterEntity.BookPageFrom = seedChapter.BookPageFrom;
            chapterEntity.BookPageTo = seedChapter.BookPageTo;
            chapterEntity.PdfPageFrom = seedChapter.PdfPageFrom;
            chapterEntity.PdfPageTo = seedChapter.PdfPageTo;
            chaptersByNumber[seedChapter.Number] = chapterEntity;
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var existingChunks = await _db.Chunk
            .Where(chunk => chunk.CorpusId == corpusEntity.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var chunksByKey = existingChunks
            .ToDictionary(chunk => (chunk.ChapterId, chunk.ChunkIndex));

        foreach (var group in chunks.GroupBy(chunk => chunk.ChapterNumber).OrderBy(group => group.Key))
        {
            if (!chaptersByNumber.TryGetValue(group.Key, out var chapterEntity))
            {
                throw new SeedLoadException(
                    $"Chunk thuộc chương không có trong build.json: {group.Key}.");
            }

            var ordered = group.OrderBy(chunk => chunk.ChunkIndex).ToList();
            foreach (var seedChunk in ordered)
            {
                if (!chunksByKey.TryGetValue((chapterEntity.Id, seedChunk.ChunkIndex), out var chunkEntity))
                {
                    chunkEntity = new Chunk
                    {
                        CorpusId = corpusEntity.Id,
                        ChapterId = chapterEntity.Id,
                        ChunkIndex = seedChunk.ChunkIndex,
                        Embedding = new Vector(seedChunk.Embedding!),
                    };
                    _db.Chunk.Add(chunkEntity);
                }

                chunkEntity.ChapterNumber = seedChunk.ChapterNumber;
                chunkEntity.Content = seedChunk.Content!;
                chunkEntity.TokenCount = seedChunk.TokenCount;
                chunkEntity.ContentHash = seedChunk.ContentHash!;
                chunkEntity.BookPageFrom = seedChunk.BookPageFrom;
                chunkEntity.BookPageTo = seedChunk.BookPageTo;
                chunkEntity.PdfPageFrom = seedChunk.PdfPageFrom;
                chunkEntity.PdfPageTo = seedChunk.PdfPageTo;
                chunkEntity.Verified = seedChunk.Verified;
                chunkEntity.NeedsReview = seedChunk.NeedsReview;
                chunkEntity.Embedding = new Vector(seedChunk.Embedding!);
            }

            // Chunker đổi số đoạn thì xoá phần dư của cùng version (giống pipeline Python).
            var extras = existingChunks
                .Where(chunk => chunk.ChapterId == chapterEntity.Id && chunk.ChunkIndex >= ordered.Count)
                .ToList();
            foreach (var extra in extras)
            {
                _db.Chunk.Remove(extra);
                chunksByKey.Remove((extra.ChapterId, extra.ChunkIndex));
            }
        }

        var existingFlags = await _db.PageFlag
            .Where(flag => flag.CorpusId == corpusEntity.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var flagsByKey = existingFlags
            .ToDictionary(flag => (flag.PdfPage, flag.Kind, flag.SourceFile));

        foreach (var seedFlag in pageFlags)
        {
            var key = (seedFlag.PdfPage, seedFlag.Kind, seedFlag.SourceFile);
            if (!flagsByKey.TryGetValue(key, out var flagEntity))
            {
                flagEntity = new PageFlag
                {
                    CorpusId = corpusEntity.Id,
                    PdfPage = seedFlag.PdfPage,
                    Kind = seedFlag.Kind,
                    SourceFile = seedFlag.SourceFile,
                };
                _db.PageFlag.Add(flagEntity);
            }

            flagEntity.BookPage = seedFlag.BookPage;
            flagEntity.Note = seedFlag.Note;
        }

        corpusEntity.ChapterCount = chapters.Count;
        corpusEntity.ChunkCount = chunks.Count;

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

        return corpusEntity.Id;
    }

    private async Task<SeedVerification> VerifyAsync(SeedBuild build, CancellationToken cancellationToken)
    {
        var corpus = await _db.Corpus
            .AsNoTracking()
            .FirstOrDefaultAsync(
                entity => entity.CorpusKey == build.CorpusKey && entity.IsActive,
                cancellationToken)
            .ConfigureAwait(false);

        if (corpus is null)
        {
            return new SeedVerification
            {
                Ok = false,
                CorpusKey = build.CorpusKey!,
                Version = build.Version!,
                Failures = [$"Không có corpus active cho '{build.CorpusKey}'."],
            };
        }

        var activeCount = await _db.Corpus
            .CountAsync(
                entity => entity.CorpusKey == build.CorpusKey && entity.IsActive,
                cancellationToken)
            .ConfigureAwait(false);
        var chapterCount = await _db.Chapter
            .CountAsync(chapter => chapter.CorpusId == corpus.Id, cancellationToken)
            .ConfigureAwait(false);
        var chunkCount = await _db.Chunk
            .CountAsync(chunk => chunk.CorpusId == corpus.Id, cancellationToken)
            .ConfigureAwait(false);
        var needsReview = await _db.Chunk
            .CountAsync(chunk => chunk.CorpusId == corpus.Id && chunk.NeedsReview, cancellationToken)
            .ConfigureAwait(false);
        var verified = await _db.Chunk
            .CountAsync(chunk => chunk.CorpusId == corpus.Id && chunk.Verified, cancellationToken)
            .ConfigureAwait(false);
        var pageFlagCount = await _db.PageFlag
            .CountAsync(flag => flag.CorpusId == corpus.Id, cancellationToken)
            .ConfigureAwait(false);

        var embeddings = await _db.Chunk
            .AsNoTracking()
            .Where(chunk => chunk.CorpusId == corpus.Id)
            .Select(chunk => chunk.Embedding)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var badDimensions = embeddings.Count(
            embedding => embedding.ToArray().Length != corpus.EmbeddingDim);

        var badHashes = await _db.Chunk
            .CountAsync(
                chunk => chunk.CorpusId == corpus.Id
                    && (chunk.ContentHash == null || chunk.ContentHash.Length != Sha256HexLength),
                cancellationToken)
            .ConfigureAwait(false);

        var badRanges = await _db.Chunk
            .AsNoTracking()
            .Where(chunk => chunk.CorpusId == corpus.Id)
            .Join(
                _db.Chapter.AsNoTracking(),
                chunk => chunk.ChapterId,
                chapter => chapter.Id,
                (chunk, chapter) => new { Chunk = chunk, Chapter = chapter })
            .CountAsync(
                row => row.Chunk.BookPageFrom > row.Chunk.BookPageTo
                    || row.Chunk.PdfPageFrom > row.Chunk.PdfPageTo
                    || row.Chunk.BookPageFrom < row.Chapter.BookPageFrom
                    || row.Chunk.BookPageTo > row.Chapter.BookPageTo
                    || row.Chunk.PdfPageFrom < row.Chapter.PdfPageFrom
                    || row.Chunk.PdfPageTo > row.Chapter.PdfPageTo,
                cancellationToken)
            .ConfigureAwait(false);

        var failures = new List<string>();
        if (activeCount != 1)
        {
            failures.Add($"Có {activeCount} corpus active cho '{build.CorpusKey}', yêu cầu đúng 1.");
        }

        if (chapterCount != build.ChapterCount)
        {
            failures.Add($"Số chương là {chapterCount}, seed ghi {build.ChapterCount}.");
        }

        if (chunkCount != build.ChunkCount)
        {
            failures.Add($"Số chunk là {chunkCount}, seed ghi {build.ChunkCount}.");
        }

        if (corpus.ChapterCount != chapterCount)
        {
            failures.Add($"corpus.chapter_count là {corpus.ChapterCount}, thực tế {chapterCount}.");
        }

        if (corpus.ChunkCount != chunkCount)
        {
            failures.Add($"corpus.chunk_count là {corpus.ChunkCount}, thực tế {chunkCount}.");
        }

        if (badDimensions != 0)
        {
            failures.Add($"Có {badDimensions} embedding không đúng {corpus.EmbeddingDim} chiều.");
        }

        if (badHashes != 0)
        {
            failures.Add($"Có {badHashes} content_hash không dài {Sha256HexLength} ký tự.");
        }

        if (badRanges != 0)
        {
            failures.Add($"Có {badRanges} chunk có khoảng trang không hợp lệ.");
        }

        if (pageFlagCount == 0)
        {
            failures.Add("Không có page flag nào.");
        }

        if (build.PageFlagCount is int expectedFlags && pageFlagCount != expectedFlags)
        {
            failures.Add($"Số page flag là {pageFlagCount}, seed ghi {expectedFlags}.");
        }

        return new SeedVerification
        {
            Ok = failures.Count == 0,
            CorpusKey = corpus.CorpusKey,
            Version = corpus.Version,
            EmbeddingDim = corpus.EmbeddingDim,
            Chapters = chapterCount,
            Chunks = chunkCount,
            PageFlags = pageFlagCount,
            NeedsReview = needsReview,
            Verified = verified,
            Failures = failures,
        };
    }

    private static void EnsureVerified(SeedVerification? verification)
    {
        if (verification is { Ok: false })
        {
            throw new SeedLoadException(
                "Verify seed thất bại: " + string.Join(" | ", verification.Failures));
        }
    }

    private static string SerializeChunkerParams(JsonElement? value) =>
        value is { ValueKind: JsonValueKind.Object } element ? element.GetRawText() : "{}";

    private sealed class SeedBuild
    {
        [JsonPropertyName("corpus_key")]
        public string? CorpusKey { get; set; }

        [JsonPropertyName("version")]
        public string? Version { get; set; }

        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("source_path")]
        public string? SourcePath { get; set; }

        [JsonPropertyName("chapter_count")]
        public int ChapterCount { get; set; }

        [JsonPropertyName("chunk_count")]
        public int ChunkCount { get; set; }

        [JsonPropertyName("page_flag_count")]
        public int? PageFlagCount { get; set; }

        [JsonPropertyName("embedding_model")]
        public string? EmbeddingModel { get; set; }

        [JsonPropertyName("embedding_dim")]
        public int EmbeddingDim { get; set; }

        [JsonPropertyName("chunker_params")]
        public JsonElement? ChunkerParams { get; set; }

        [JsonPropertyName("embedded_at")]
        public DateTimeOffset EmbeddedAt { get; set; }

        [JsonPropertyName("chapters")]
        public List<SeedChapter>? Chapters { get; set; }
    }

    private sealed class SeedChapter
    {
        [JsonPropertyName("number")]
        public int Number { get; set; }

        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("source_file")]
        public string? SourceFile { get; set; }

        [JsonPropertyName("book_page_from")]
        public int BookPageFrom { get; set; }

        [JsonPropertyName("book_page_to")]
        public int BookPageTo { get; set; }

        [JsonPropertyName("pdf_page_from")]
        public int PdfPageFrom { get; set; }

        [JsonPropertyName("pdf_page_to")]
        public int PdfPageTo { get; set; }
    }

    private sealed class SeedChunk
    {
        [JsonPropertyName("chapter_number")]
        public int ChapterNumber { get; set; }

        [JsonPropertyName("chunk_index")]
        public int ChunkIndex { get; set; }

        [JsonPropertyName("content")]
        public string? Content { get; set; }

        [JsonPropertyName("token_count")]
        public int TokenCount { get; set; }

        [JsonPropertyName("content_hash")]
        public string? ContentHash { get; set; }

        [JsonPropertyName("book_page_from")]
        public int BookPageFrom { get; set; }

        [JsonPropertyName("book_page_to")]
        public int BookPageTo { get; set; }

        [JsonPropertyName("pdf_page_from")]
        public int PdfPageFrom { get; set; }

        [JsonPropertyName("pdf_page_to")]
        public int PdfPageTo { get; set; }

        [JsonPropertyName("verified")]
        public bool Verified { get; set; }

        [JsonPropertyName("needs_review")]
        public bool NeedsReview { get; set; }

        [JsonPropertyName("embedding")]
        public float[]? Embedding { get; set; }
    }

    private sealed class SeedPageFlag
    {
        [JsonPropertyName("pdf_page")]
        public int PdfPage { get; set; }

        [JsonPropertyName("book_page")]
        public int BookPage { get; set; }

        [JsonPropertyName("kind")]
        public string Kind { get; set; } = string.Empty;

        [JsonPropertyName("source_file")]
        public string SourceFile { get; set; } = string.Empty;

        [JsonPropertyName("note")]
        public string? Note { get; set; }
    }
}
