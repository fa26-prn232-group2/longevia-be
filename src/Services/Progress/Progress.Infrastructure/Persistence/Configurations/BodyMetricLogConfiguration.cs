using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Progress.Domain.Entities;

namespace Progress.Infrastructure.Persistence.Configurations;

public sealed class BodyMetricLogConfiguration : IEntityTypeConfiguration<BodyMetricLog>
{
    public void Configure(EntityTypeBuilder<BodyMetricLog> builder)
    {
        builder.ToTable("body_metric_logs");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.UserId)
            .IsRequired();

        builder.Property(x => x.LoggedAt)
            .IsRequired();

        builder.Property(x => x.WeightKg)
            .HasPrecision(5, 2)
            .IsRequired();

        builder.Property(x => x.HeightUsedCm)
            .HasPrecision(5, 2)
            .IsRequired();

        builder.Property(x => x.Bmi)
            .HasPrecision(5, 2)
            .IsRequired();

        builder.Property(x => x.BmiCategory)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.BodyFatPercent)
            .HasPrecision(5, 2);

        builder.Property(x => x.WaistCircumferenceCm)
            .HasPrecision(5, 2);

        builder.Property(x => x.SystolicBloodPressure);
        builder.Property(x => x.DiastolicBloodPressure);
        builder.Property(x => x.RestingHeartRate);

        // Index tìm kiếm theo User và LoggedAt (hỗ trợ phân trang và dashboard delta)
        builder.HasIndex(x => new { x.UserId, x.LoggedAt });
    }
}
