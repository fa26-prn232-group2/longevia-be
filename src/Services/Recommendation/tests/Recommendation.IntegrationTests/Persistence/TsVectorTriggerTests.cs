using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;
using Recommendation.Infrastructure.Options;
using Recommendation.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace Recommendation.IntegrationTests.Persistence;

/// <summary>
/// T113 - R3/D-06: trigger <c>trg_chunk_content_tsv</c> phải giữ <c>content_tsv</c>
/// khớp <c>to_tsvector('simple', unaccent(content))</c> sau cả INSERT lẫn UPDATE,
/// và index GIN <c>ix_chunk_tsv</c> phải dùng được cho truy vấn <c>@@</c>.
/// </summary>
public sealed class TsVectorTriggerTests : IAsyncLifetime
{
    private const string InitialContent = "Nhịn ăn gián đoạn giúp cơ thể đốt mỡ và cải thiện giấc ngủ.";
    private const string UpdatedContent = "Giấc ngủ đều đặn và uống đủ nước hỗ trợ chế độ ăn trường thọ.";

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
    public async Task TriggerKeepsTsVectorInSyncOnInsertAndUpdateAndGinIndexIsUsable()
    {
        var connectionString = _postgres!.GetConnectionString();
        await MigrateAsync(connectionString);

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        var chunkId = await InsertChunkAsync(connection, InitialContent);
        var afterInsert = await ReadTsVectorPairAsync(connection, chunkId);

        Assert.False(string.IsNullOrWhiteSpace(afterInsert.Stored));
        Assert.Equal(afterInsert.Expected, afterInsert.Stored);

        await UpdateContentAsync(connection, chunkId, UpdatedContent);
        var afterUpdate = await ReadTsVectorPairAsync(connection, chunkId);

        Assert.NotEqual(afterInsert.Stored, afterUpdate.Stored);
        Assert.Equal(afterUpdate.Expected, afterUpdate.Stored);

        var plan = await ExplainSearchAsync(connection);
        Assert.Contains("ix_chunk_tsv", plan, StringComparison.Ordinal);
        Assert.Contains("Bitmap Index Scan", plan, StringComparison.Ordinal);
    }

    private static async Task MigrateAsync(string connectionString)
    {
        var knowledgeOptions = Options.Create(new KnowledgeDbOptions
        {
            ConnectionString = connectionString,
            CorpusKey = "che-do-an-truong-tho",
            Schema = "kb",
        });
        var dbOptions = new DbContextOptionsBuilder<KnowledgeDbContext>()
            .UseNpgsql(connectionString, npgsql => npgsql.UseVector())
            .Options;

        await using var db = new KnowledgeDbContext(dbOptions, knowledgeOptions);
        await db.Database.MigrateAsync();
    }

    private static async Task<long> InsertChunkAsync(NpgsqlConnection connection, string content)
    {
        var corpusId = Guid.NewGuid();
        var chapterId = Guid.NewGuid();

        await using var command = new NpgsqlCommand(
            """
            INSERT INTO kb.corpus (
                id, corpus_key, version, title, source_path,
                chapter_count, chunk_count, embedding_model, embedding_dim,
                chunker_params, is_active, embedded_at, created_at
            )
            VALUES (
                @corpusId, 'che-do-an-truong-tho', 'trigger-test', 'Trigger test', 'docs/reference',
                1, 1, 'gemini-embedding-001', 768,
                '{}'::jsonb, true, now(), now()
            );

            INSERT INTO kb.chapter (
                id, corpus_id, chapter_number, title, source_file,
                book_page_from, book_page_to, pdf_page_from, pdf_page_to
            )
            VALUES (
                @chapterId, @corpusId, 4, 'Chế độ ăn Trường thọ', '04-chuong-4.md',
                69, 105, 61, 98
            );

            INSERT INTO kb.chunk (
                corpus_id, chapter_id, chapter_number, chunk_index,
                content, token_count, content_hash,
                book_page_from, book_page_to, pdf_page_from, pdf_page_to,
                verified, needs_review, embedding
            )
            VALUES (
                @corpusId, @chapterId, 4, 0,
                @content, 10, repeat('a', 64),
                69, 69, 61, 61,
                true, false, CAST(@embedding AS vector)
            )
            RETURNING id;
            """,
            connection);
        command.Parameters.AddWithValue("corpusId", corpusId);
        command.Parameters.AddWithValue("chapterId", chapterId);
        command.Parameters.AddWithValue("content", content);
        command.Parameters.AddWithValue("embedding", BuildVectorLiteral());

        return (long)(await command.ExecuteScalarAsync())!;
    }

    private static async Task UpdateContentAsync(NpgsqlConnection connection, long chunkId, string content)
    {
        await using var command = new NpgsqlCommand(
            "UPDATE kb.chunk SET content = @content WHERE id = @id;",
            connection);
        command.Parameters.AddWithValue("content", content);
        command.Parameters.AddWithValue("id", chunkId);

        Assert.Equal(1, await command.ExecuteNonQueryAsync());
    }

    private static async Task<(string Stored, string Expected)> ReadTsVectorPairAsync(
        NpgsqlConnection connection,
        long chunkId)
    {
        await using var command = new NpgsqlCommand(
            """
            SELECT content_tsv::text,
                   to_tsvector('simple', unaccent(content))::text
            FROM kb.chunk
            WHERE id = @id;
            """,
            connection);
        command.Parameters.AddWithValue("id", chunkId);

        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());

        return (reader.GetString(0), reader.GetString(1));
    }

    private static async Task<string> ExplainSearchAsync(NpgsqlConnection connection)
    {
        // Bảng test rất nhỏ nên planner có thể chọn seq scan vì cost thấp; tắt
        // seq scan để chứng minh index GIN tồn tại và dùng được cho toán tử @@.
        await using var transaction = await connection.BeginTransactionAsync();

        await using (var setup = new NpgsqlCommand(
            "SET LOCAL enable_seqscan = off; SET LOCAL enable_bitmapscan = on;",
            connection,
            transaction))
        {
            await setup.ExecuteNonQueryAsync();
        }

        await using var command = new NpgsqlCommand(
            """
            EXPLAIN (FORMAT TEXT)
            SELECT id
            FROM kb.chunk
            WHERE content_tsv @@ plainto_tsquery('simple', unaccent('nhin an giac ngu'));
            """,
            connection,
            transaction);

        var lines = new List<string>();
        await using (var reader = await command.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                lines.Add(reader.GetString(0));
            }
        }

        await transaction.RollbackAsync();
        return string.Join('\n', lines);
    }

    private static string BuildVectorLiteral()
    {
        var values = new string[768];
        Array.Fill(values, "0");
        values[0] = "1";
        return "[" + string.Join(',', values) + "]";
    }
}
