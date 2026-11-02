using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Progress.Domain.Entities;

namespace Progress.Infrastructure.Persistence.Configurations;

public sealed class SleepLogConfiguration : IEntityTypeConfiguration<SleepLog>
{
    public void Configure(EntityTypeBuilder<SleepLog> builder)
    {
        builder.ToTable("sleep_logs");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.UserId)
            .IsRequired();

        builder.Property(x => x.SleptAt)
            .IsRequired();

        builder.Property(x => x.WokeAt)
            .IsRequired();

        builder.Property(x => x.SleepQuality)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.DurationHours)
            .IsRequired();

        builder.Property(x => x.Date)
            .IsRequired();

        builder.HasIndex(x => new { x.UserId, x.Date });
        builder.HasIndex(x => new { x.UserId, x.WokeAt });
    }
}
