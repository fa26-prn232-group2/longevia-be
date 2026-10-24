using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Progress.Domain.Entities;

namespace Progress.Infrastructure.Persistence.Configurations;

public sealed class FastingSessionConfiguration : IEntityTypeConfiguration<FastingSession>
{
    public void Configure(EntityTypeBuilder<FastingSession> builder)
    {
        builder.ToTable("fasting_sessions");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.UserId)
            .IsRequired();

        builder.Property(x => x.Protocol)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.TargetHours)
            .IsRequired();

        builder.Property(x => x.StartedAt)
            .IsRequired();

        builder.Property(x => x.EndedAt);

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        // Index tra cứu lịch sử theo user và thời gian
        builder.HasIndex(x => new { x.UserId, x.StartedAt });

        // Filtered unique index: Mỗi người dùng chỉ được có tối đa 1 phiên active (EndedAt IS NULL)
        builder.HasIndex(x => x.UserId)
            .HasFilter("\"EndedAt\" IS NULL")
            .IsUnique();
    }
}
