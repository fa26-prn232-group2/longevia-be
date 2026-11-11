using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Progress.Domain.Entities;

namespace Progress.Infrastructure.Persistence.Configurations;

public sealed class ProcessedMealRemovalConfiguration : IEntityTypeConfiguration<ProcessedMealRemoval>
{
    public void Configure(EntityTypeBuilder<ProcessedMealRemoval> builder)
    {
        builder.ToTable("processed_meal_removals");

        builder.HasKey(x => x.EventId);

        builder.Property(x => x.MealLogId).IsRequired();
        builder.Property(x => x.UserId).IsRequired();
        builder.Property(x => x.BusinessDate).IsRequired();
        builder.Property(x => x.ProcessedAt).IsRequired();

        // Một MealLog chỉ bị xoá một lần; unique để chặn hai delivery song song.
        builder.HasIndex(x => x.MealLogId).IsUnique();
        builder.HasIndex(x => new { x.UserId, x.BusinessDate });
    }
}
