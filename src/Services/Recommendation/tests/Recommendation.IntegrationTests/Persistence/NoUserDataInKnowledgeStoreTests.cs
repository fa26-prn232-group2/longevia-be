using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;
using Recommendation.Infrastructure.Options;
using Recommendation.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace Recommendation.IntegrationTests.Persistence;

/// <summary>
/// T112 - SC-009: <c>recommendation_db</c> chỉ chứa corpus tri thức. Test đọc
/// <c>information_schema.columns</c> của schema <c>kb</c> trên PostgreSQL thật và
/// khẳng định schema chỉ có đúng 4 bảng, không bảng/cột nào mang dữ liệu hồ sơ
/// hay nhật ký người dùng.
/// </summary>
public sealed class NoUserDataInKnowledgeStoreTests : IAsyncLifetime
{
    private const string Schema = "kb";

    private static readonly string[] ExpectedTables = ["chapter", "chunk", "corpus", "page_flag"];

    // So khớp CHÍNH XÁC tên cột (snake_case), không so khớp chuỗi con: cột
    // `token_count` là số token của đoạn văn, không phải token xác thực.
    private static readonly HashSet<string> ForbiddenColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        "user_id",
        "userid",
        "email",
        "profile",
        "profiles",
        "allergy",
        "allergies",
        "token",
        "access_token",
        "refresh_token",
        "token_hash",
        "password",
        "password_hash",
        "weight",
        "weight_kg",
        "height",
        "height_cm",
        "birth",
        "birth_date",
        "date_of_birth",
        "birthday",
        "meal",
        "meal_id",
        "meal_log",
        "log",
        "user_log",
        "session",
        "session_id",
    };

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
    public async Task KnowledgeSchemaHoldsOnlyCorpusTablesWithoutUserDataColumns()
    {
        var columns = await ReadSchemaColumnsAsync();

        var tables = columns
            .Select(column => column.TableName)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(ExpectedTables, tables);

        var offending = columns
            .Where(column => ForbiddenColumns.Contains(column.ColumnName))
            .Select(column => $"{column.TableName}.{column.ColumnName}")
            .ToArray();

        Assert.True(
            offending.Length == 0,
            "Schema kb chứa cột dữ liệu người dùng: " + string.Join(", ", offending));

        Assert.All(
            columns,
            column => Assert.False(
                column.ColumnName.StartsWith("user_", StringComparison.OrdinalIgnoreCase),
                $"Cột {column.TableName}.{column.ColumnName} mang tiền tố dữ liệu người dùng."));
    }

    private async Task<IReadOnlyList<SchemaColumn>> ReadSchemaColumnsAsync()
    {
        var connectionString = _postgres!.GetConnectionString();
        var knowledgeOptions = Options.Create(new KnowledgeDbOptions
        {
            ConnectionString = connectionString,
            CorpusKey = "che-do-an-truong-tho",
            Schema = Schema,
        });
        var dbOptions = new DbContextOptionsBuilder<KnowledgeDbContext>()
            .UseNpgsql(connectionString, npgsql => npgsql.UseVector())
            .Options;

        await using (var db = new KnowledgeDbContext(dbOptions, knowledgeOptions))
        {
            await db.Database.MigrateAsync();
        }

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            """
            SELECT table_name, column_name
            FROM information_schema.columns
            WHERE table_schema = @schema
            ORDER BY table_name, ordinal_position;
            """,
            connection);
        command.Parameters.AddWithValue("schema", Schema);

        var columns = new List<SchemaColumn>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            columns.Add(new SchemaColumn(reader.GetString(0), reader.GetString(1)));
        }

        return columns;
    }

    private sealed record SchemaColumn(string TableName, string ColumnName);
}
