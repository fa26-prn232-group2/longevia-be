using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Progress.Domain.Entities;

namespace Progress.Infrastructure.Persistence.Configurations;

public sealed class WorkoutPlanConfiguration : IEntityTypeConfiguration<WorkoutPlan>
{
    public void Configure(EntityTypeBuilder<WorkoutPlan> builder)
    {
        builder.ToTable("workout_plans");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.UserId, x.WeekStartDate }).IsUnique();
        builder.Property(x => x.Goal).HasMaxLength(30).IsRequired();
        builder.Property(x => x.PlanJson).HasColumnType("jsonb").IsRequired();
    }
}
