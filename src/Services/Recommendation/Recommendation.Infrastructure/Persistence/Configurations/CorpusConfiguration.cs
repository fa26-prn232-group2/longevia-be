using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Recommendation.Infrastructure.Persistence.Entities;

namespace Recommendation.Infrastructure.Persistence.Configurations;

/// <summary>
/// Cấu hình ánh xạ bảng <c>kb.corpus</c>.
/// </summary>
public sealed class CorpusConfiguration : IEntityTypeConfiguration<Corpus>
{
    public void Configure(EntityTypeBuilder<Corpus> builder)
    {
        builder.ToTable("corpus", "kb");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(c => c.CorpusKey).HasColumnName("corpus_key").HasMaxLength(200).IsRequired();
        builder.Property(c => c.Version).HasColumnName("version").HasMaxLength(128).IsRequired();
        builder.Property(c => c.Title).HasColumnName("title").HasMaxLength(500).IsRequired();
        builder.Property(c => c.SourcePath).HasColumnName("source_path").HasMaxLength(500).IsRequired();
        builder.Property(c => c.ChapterCount).HasColumnName("chapter_count").IsRequired();
        builder.Property(c => c.ChunkCount).HasColumnName("chunk_count").IsRequired();
        builder.Property(c => c.EmbeddingModel).HasColumnName("embedding_model").HasMaxLength(200).IsRequired();
        builder.Property(c => c.EmbeddingDim).HasColumnName("embedding_dim").IsRequired();
        builder.Property(c => c.ChunkerParams).HasColumnName("chunker_params").HasColumnType("jsonb").IsRequired();
        builder.Property(c => c.IsActive).HasColumnName("is_active").HasDefaultValue(false).IsRequired();
        builder.Property(c => c.EmbeddedAt).HasColumnName("embedded_at").IsRequired();
        builder.Property(c => c.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()").IsRequired();

        // R2: nạp lại cùng version không tạo bản ghi trùng.
        builder.HasIndex(c => new { c.CorpusKey, c.Version })
            .IsUnique()
            .HasDatabaseName("ux_corpus_key_version");

        // R2: chỉ một phiên bản active cho mỗi corpus_key.
        builder.HasIndex(c => c.CorpusKey)
            .IsUnique()
            .HasFilter("is_active")
            .HasDatabaseName("ux_corpus_active");
    }
}
