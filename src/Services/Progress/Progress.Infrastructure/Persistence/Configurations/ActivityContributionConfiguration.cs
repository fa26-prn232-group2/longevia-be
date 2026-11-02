using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Progress.Domain.Entities;

namespace Progress.Infrastructure.Persistence.Configurations;

public sealed class ActivityContributionConfiguration : IEntityTypeConfiguration<ActivityContribution>
{
    public void Configure(EntityTypeBuilder<ActivityContribution> builder)
    {
        builder.ToTable("activity_contributions");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.UserId)
            .IsRequired();

        builder.Property(x => x.Source)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.SourceLogId)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.Date)
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        // Unique constraint: mỗi Source + SourceLogId chỉ có 1 bản ghi
        builder.HasIndex(x => new { x.Source, x.SourceLogId })
            .IsUnique();

        builder.HasIndex(x => new { x.UserId, x.Date });
    }
}
