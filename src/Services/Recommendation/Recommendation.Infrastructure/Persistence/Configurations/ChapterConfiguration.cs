using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Recommendation.Infrastructure.Persistence.Entities;

namespace Recommendation.Infrastructure.Persistence.Configurations;

/// <summary>
/// Cấu hình ánh xạ bảng <c>kb.chapter</c>.
/// </summary>
public sealed class ChapterConfiguration : IEntityTypeConfiguration<Chapter>
{
    public void Configure(EntityTypeBuilder<Chapter> builder)
    {
        builder.ToTable("chapter", "kb");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(c => c.CorpusId).HasColumnName("corpus_id").IsRequired();
        builder.Property(c => c.ChapterNumber).HasColumnName("chapter_number").IsRequired();
        builder.Property(c => c.Title).HasColumnName("title").HasMaxLength(500).IsRequired();
        builder.Property(c => c.SourceFile).HasColumnName("source_file").HasMaxLength(500).IsRequired();
        builder.Property(c => c.BookPageFrom).HasColumnName("book_page_from").IsRequired();
        builder.Property(c => c.BookPageTo).HasColumnName("book_page_to").IsRequired();
        builder.Property(c => c.PdfPageFrom).HasColumnName("pdf_page_from").IsRequired();
        builder.Property(c => c.PdfPageTo).HasColumnName("pdf_page_to").IsRequired();

        builder.HasOne(c => c.Corpus)
            .WithMany(c => c.Chapters)
            .HasForeignKey(c => c.CorpusId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_chapter_corpus");

        builder.HasIndex(c => new { c.CorpusId, c.ChapterNumber })
            .IsUnique()
            .HasDatabaseName("ux_chapter_corpus_number");
    }
}
