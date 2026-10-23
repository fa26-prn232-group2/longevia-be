using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Recommendation.Infrastructure.Persistence.Entities;

namespace Recommendation.Infrastructure.Persistence.Configurations;

/// <summary>
/// Cấu hình ánh xạ bảng <c>kb.page_flag</c>.
/// </summary>
public sealed class PageFlagConfiguration : IEntityTypeConfiguration<PageFlag>
{
    public void Configure(EntityTypeBuilder<PageFlag> builder)
    {
        builder.ToTable("page_flag", "kb");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("id").ValueGeneratedOnAdd();

        builder.Property(p => p.CorpusId).HasColumnName("corpus_id").IsRequired();
        builder.Property(p => p.PdfPage).HasColumnName("pdf_page").IsRequired();
        builder.Property(p => p.BookPage).HasColumnName("book_page").IsRequired();
        builder.Property(p => p.Kind).HasColumnName("kind").HasMaxLength(100).IsRequired();
        builder.Property(p => p.SourceFile).HasColumnName("source_file").HasMaxLength(500).IsRequired();
        builder.Property(p => p.Note).HasColumnName("note").HasMaxLength(1000);

        builder.HasOne(p => p.Corpus)
            .WithMany(c => c.PageFlags)
            .HasForeignKey(p => p.CorpusId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_page_flag_corpus");

        builder.HasIndex(p => new { p.CorpusId, p.PdfPage, p.Kind, p.SourceFile })
            .IsUnique()
            .HasDatabaseName("ux_page_flag_corpus_page_kind_source");
    }
}
