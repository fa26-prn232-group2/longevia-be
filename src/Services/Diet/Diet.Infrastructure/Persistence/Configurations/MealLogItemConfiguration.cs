using Diet.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diet.Infrastructure.Persistence.Configurations;

public sealed class MealLogItemConfiguration : IEntityTypeConfiguration<MealLogItem>
{
    public void Configure(EntityTypeBuilder<MealLogItem> builder)
    {
        builder.ToTable("meal_log_items", table => table.HasCheckConstraint("CK_meal_quantity", "\"Quantity\" > 0"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.ServingUnit).HasMaxLength(30).IsRequired();
        builder.Property(x => x.Quantity).HasPrecision(12, 3);
        builder.HasOne<FoodItem>().WithMany().HasForeignKey(x => x.FoodId).OnDelete(DeleteBehavior.Restrict);
        builder.OwnsOne(x => x.Nutrition, NutritionMapping.Configure);
        builder.Navigation(x => x.Nutrition).IsRequired();
    }
}
