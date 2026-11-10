using Diet.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diet.Infrastructure.Persistence.Configurations;

public sealed class MealPlanConfiguration : IEntityTypeConfiguration<MealPlan>
{
    public void Configure(EntityTypeBuilder<MealPlan> builder)
    {
        builder.ToTable("meal_plans");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.UserId, x.WeekStartDate }).IsUnique();
        builder.Property(x => x.PlanJson).HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.WeekStartDate).IsRequired();
    }
}
