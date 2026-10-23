using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using NpgsqlTypes;
using Pgvector;

#nullable disable

namespace Recommendation.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialKnowledgeStore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "kb");

            // Extension `unaccent` cho trigger tsvector (D-06). `vector` được tạo
            // bởi HasPostgresExtension("vector") ở trên (Code First).
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS unaccent;");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:vector", ",,");

            migrationBuilder.CreateTable(
                name: "corpus",
                schema: "kb",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    corpus_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    version = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    source_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    chapter_count = table.Column<int>(type: "integer", nullable: false),
                    chunk_count = table.Column<int>(type: "integer", nullable: false),
                    embedding_model = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    embedding_dim = table.Column<int>(type: "integer", nullable: false),
                    chunker_params = table.Column<string>(type: "jsonb", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    embedded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_corpus", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "chapter",
                schema: "kb",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    corpus_id = table.Column<Guid>(type: "uuid", nullable: false),
                    chapter_number = table.Column<int>(type: "integer", nullable: false),
                    title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    source_file = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    book_page_from = table.Column<int>(type: "integer", nullable: false),
                    book_page_to = table.Column<int>(type: "integer", nullable: false),
                    pdf_page_from = table.Column<int>(type: "integer", nullable: false),
                    pdf_page_to = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chapter", x => x.id);
                    table.ForeignKey(
                        name: "fk_chapter_corpus",
                        column: x => x.corpus_id,
                        principalSchema: "kb",
                        principalTable: "corpus",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "page_flag",
                schema: "kb",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    corpus_id = table.Column<Guid>(type: "uuid", nullable: false),
                    pdf_page = table.Column<int>(type: "integer", nullable: false),
                    book_page = table.Column<int>(type: "integer", nullable: false),
                    kind = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    source_file = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_page_flag", x => x.id);
                    table.ForeignKey(
                        name: "fk_page_flag_corpus",
                        column: x => x.corpus_id,
                        principalSchema: "kb",
                        principalTable: "corpus",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "chunk",
                schema: "kb",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    corpus_id = table.Column<Guid>(type: "uuid", nullable: false),
                    chapter_id = table.Column<Guid>(type: "uuid", nullable: false),
                    chapter_number = table.Column<int>(type: "integer", nullable: false),
                    chunk_index = table.Column<int>(type: "integer", nullable: false),
                    content = table.Column<string>(type: "text", nullable: false),
                    content_tsv = table.Column<NpgsqlTsVector>(type: "tsvector", nullable: false),
                    token_count = table.Column<int>(type: "integer", nullable: false),
                    content_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    book_page_from = table.Column<int>(type: "integer", nullable: false),
                    book_page_to = table.Column<int>(type: "integer", nullable: false),
                    pdf_page_from = table.Column<int>(type: "integer", nullable: false),
                    pdf_page_to = table.Column<int>(type: "integer", nullable: false),
                    verified = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    needs_review = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    embedding = table.Column<Vector>(type: "vector(768)", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chunk", x => x.id);
                    table.ForeignKey(
                        name: "fk_chunk_chapter",
                        column: x => x.chapter_id,
                        principalSchema: "kb",
                        principalTable: "chapter",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_chunk_corpus",
                        column: x => x.corpus_id,
                        principalSchema: "kb",
                        principalTable: "corpus",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Trigger duy trì `content_tsv` (R3): `unaccent` là STABLE nên không
            // dùng được cho generated column / expression index — trigger là cách
            // đúng (D-06).
            migrationBuilder.Sql(
                """
                CREATE OR REPLACE FUNCTION kb.chunk_content_tsv_update() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    NEW.content_tsv := to_tsvector('simple', unaccent(NEW.content));
                    RETURN NEW;
                END;
                $$;

                CREATE TRIGGER trg_chunk_content_tsv
                    BEFORE INSERT OR UPDATE OF content ON kb.chunk
                    FOR EACH ROW EXECUTE FUNCTION kb.chunk_content_tsv_update();
                """);

            migrationBuilder.CreateIndex(
                name: "ux_chapter_corpus_number",
                schema: "kb",
                table: "chapter",
                columns: new[] { "corpus_id", "chapter_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_chunk_chapter",
                schema: "kb",
                table: "chunk",
                columns: new[] { "corpus_id", "chapter_number" });

            migrationBuilder.CreateIndex(
                name: "IX_chunk_chapter_id",
                schema: "kb",
                table: "chunk",
                column: "chapter_id");

            migrationBuilder.CreateIndex(
                name: "ix_chunk_corpus",
                schema: "kb",
                table: "chunk",
                column: "corpus_id");

            migrationBuilder.CreateIndex(
                name: "ix_chunk_embedding_hnsw",
                schema: "kb",
                table: "chunk",
                column: "embedding")
                .Annotation("Npgsql:IndexMethod", "hnsw")
                .Annotation("Npgsql:IndexOperators", new[] { "vector_cosine_ops" })
                .Annotation("Npgsql:StorageParameter:ef_construction", 64)
                .Annotation("Npgsql:StorageParameter:m", 16);

            migrationBuilder.CreateIndex(
                name: "ix_chunk_pages",
                schema: "kb",
                table: "chunk",
                columns: new[] { "book_page_from", "book_page_to" });

            migrationBuilder.CreateIndex(
                name: "ix_chunk_tsv",
                schema: "kb",
                table: "chunk",
                column: "content_tsv")
                .Annotation("Npgsql:IndexMethod", "gin");

            migrationBuilder.CreateIndex(
                name: "ux_chunk_corpus_chapter_index",
                schema: "kb",
                table: "chunk",
                columns: new[] { "corpus_id", "chapter_id", "chunk_index" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_corpus_active",
                schema: "kb",
                table: "corpus",
                column: "corpus_key",
                unique: true,
                filter: "is_active");

            migrationBuilder.CreateIndex(
                name: "ux_corpus_key_version",
                schema: "kb",
                table: "corpus",
                columns: new[] { "corpus_key", "version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_page_flag_corpus_page_kind_source",
                schema: "kb",
                table: "page_flag",
                columns: new[] { "corpus_id", "pdf_page", "kind", "source_file" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DROP TRIGGER IF EXISTS trg_chunk_content_tsv ON kb.chunk;
                DROP FUNCTION IF EXISTS kb.chunk_content_tsv_update();
                DROP EXTENSION IF EXISTS unaccent;
                """);

            migrationBuilder.DropTable(
                name: "chunk",
                schema: "kb");

            migrationBuilder.DropTable(
                name: "page_flag",
                schema: "kb");

            migrationBuilder.DropTable(
                name: "chapter",
                schema: "kb");

            migrationBuilder.DropTable(
                name: "corpus",
                schema: "kb");
        }
    }
}
