using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Recommendation.Infrastructure.Persistence.Entities;

namespace Recommendation.Infrastructure.Persistence.Configurations;

/// <summary>
/// Cấu hình ánh xạ bảng <c>kb.chunk</c> — đơn vị truy xuất của RAG.
/// </summary>
public sealed class ChunkConfiguration : IEntityTypeConfiguration<Chunk>
{
    public void Configure(EntityTypeBuilder<Chunk> builder)
    {
        builder.ToTable("chunk", "kb");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("id").ValueGeneratedOnAdd();

        builder.Property(c => c.CorpusId).HasColumnName("corpus_id").IsRequired();
        builder.Property(c => c.ChapterId).HasColumnName("chapter_id").IsRequired();
        builder.Property(c => c.ChapterNumber).HasColumnName("chapter_number").IsRequired();
        builder.Property(c => c.ChunkIndex).HasColumnName("chunk_index").IsRequired();
        builder.Property(c => c.Content).HasColumnName("content").IsRequired();

        // Do trigger trong migration duy trì (R3): KHÔNG gán tay trong code và
        // không cho EF ghi xuống — `BeforeSaveBehavior.Ignore` loại cột khỏi
        // câu lệnh INSERT/UPDATE.
        builder.Property(c => c.ContentTsv)
            .HasColumnName("content_tsv")
            .HasColumnType("tsvector")
            .IsRequired()
            .Metadata.SetBeforeSaveBehavior(PropertySaveBehavior.Ignore);

        builder.Property(c => c.TokenCount).HasColumnName("token_count").IsRequired();
        builder.Property(c => c.ContentHash).HasColumnName("content_hash").HasMaxLength(128).IsRequired();
        builder.Property(c => c.BookPageFrom).HasColumnName("book_page_from").IsRequired();
        builder.Property(c => c.BookPageTo).HasColumnName("book_page_to").IsRequired();
        builder.Property(c => c.PdfPageFrom).HasColumnName("pdf_page_from").IsRequired();
        builder.Property(c => c.PdfPageTo).HasColumnName("pdf_page_to").IsRequired();
        builder.Property(c => c.Verified).HasColumnName("verified").HasDefaultValue(false).IsRequired();
        builder.Property(c => c.NeedsReview).HasColumnName("needs_review").HasDefaultValue(false).IsRequired();

        // Kich thuoc vector phai khop voi RagOptions.EmbeddingDim (nguon suy nhat).
        builder.Property(c => c.Embedding).HasColumnName("embedding").HasColumnType("vector(768)").IsRequired();

        builder.Property(c => c.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()").IsRequired();

        builder.HasOne(c => c.Corpus)
            .WithMany(c => c.Chunks)
            .HasForeignKey(c => c.CorpusId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_chunk_corpus");

        builder.HasOne(c => c.Chapter)
            .WithMany(c => c.Chunks)
            .HasForeignKey(c => c.ChapterId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_chunk_chapter");

        // R1: nạp lại cùng version không tạo đoạn trùng.
        builder.HasIndex(c => new { c.CorpusId, c.ChapterId, c.ChunkIndex })
            .IsUnique()
            .HasDatabaseName("ux_chunk_corpus_chapter_index");

        builder.HasIndex(c => c.CorpusId).HasDatabaseName("ix_chunk_corpus");
        builder.HasIndex(c => new { c.CorpusId, c.ChapterNumber }).HasDatabaseName("ix_chunk_chapter");
        builder.HasIndex(c => new { c.BookPageFrom, c.BookPageTo }).HasDatabaseName("ix_chunk_pages");

        // Keyword search: tsvector + GIN (D-06).
        builder.HasIndex(c => c.ContentTsv)
            .HasMethod("gin")
            .HasDatabaseName("ix_chunk_tsv");

        // Vector search: cosine + HNSW (D-05, D-11).
        builder.HasIndex(c => c.Embedding)
            .HasMethod("hnsw")
            .HasOperators("vector_cosine_ops")
            .HasStorageParameter("m", 16)
            .HasStorageParameter("ef_construction", 64)
            .HasDatabaseName("ix_chunk_embedding_hnsw");
    }
}
