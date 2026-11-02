using Diet.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diet.Infrastructure.Persistence.Configurations;

public sealed class DailyDietTargetConfiguration : IEntityTypeConfiguration<DailyDietTarget>
{
    public void Configure(EntityTypeBuilder<DailyDietTarget> builder)
    {
        builder.ToTable("daily_diet_targets");
        builder.HasKey(x => x.Id);
        builder.OwnsOne(x => x.Calculation, snapshot =>
        {
            snapshot.Property(x => x.WeightKg).HasPrecision(8, 2);
            snapshot.Property(x => x.HeightCm).HasPrecision(5, 2);
            snapshot.Property(x => x.ActivityFactor).HasPrecision(6, 4);
            snapshot.Property(x => x.Bmr).HasPrecision(18, 8);
            snapshot.Property(x => x.Tdee).HasPrecision(18, 8);
        });
        builder.HasIndex(x => x.UserId).IsUnique();
        builder.Property(x => x.TargetCalories).HasPrecision(12, 3);
        builder.Property(x => x.TargetProteinGrams).HasPrecision(12, 3);
        builder.Property(x => x.TargetCarbsGrams).HasPrecision(12, 3);
        builder.Property(x => x.TargetFatGrams).HasPrecision(12, 3);
    }
}
