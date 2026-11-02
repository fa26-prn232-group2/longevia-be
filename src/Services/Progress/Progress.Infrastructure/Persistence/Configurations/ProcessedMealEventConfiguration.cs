using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Progress.Domain.Entities;

namespace Progress.Infrastructure.Persistence.Configurations;

public sealed class ProcessedMealEventConfiguration : IEntityTypeConfiguration<ProcessedMealEvent>
{
    public void Configure(EntityTypeBuilder<ProcessedMealEvent> builder)
    {
        builder.ToTable("processed_meal_events");

        builder.HasKey(x => x.EventId);

        builder.Property(x => x.MealLogId)
            .IsRequired();

        builder.Property(x => x.UserId)
            .IsRequired();

        builder.Property(x => x.BusinessDate)
            .IsRequired();

        builder.Property(x => x.ProcessedAt)
            .IsRequired();

        builder.HasIndex(x => x.MealLogId);

        builder.HasIndex(x => new { x.UserId, x.BusinessDate });
    }
}
