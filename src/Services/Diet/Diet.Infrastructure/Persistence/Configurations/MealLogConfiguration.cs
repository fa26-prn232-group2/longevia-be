using Diet.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diet.Infrastructure.Persistence.Configurations;

public sealed class MealLogConfiguration : IEntityTypeConfiguration<MealLog>
{
    public void Configure(EntityTypeBuilder<MealLog> builder)
    {
        builder.ToTable("meal_logs");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.UserId, x.Date });
        builder.Property(x => x.MealType).HasMaxLength(20).IsRequired();
        builder.HasMany(x => x.Items).WithOne().HasForeignKey(x => x.MealLogId).OnDelete(DeleteBehavior.Cascade);
    }
}
