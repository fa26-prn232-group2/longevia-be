using System.Text.Json;
using Diet.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diet.Infrastructure.Persistence.Configurations;

public sealed class FoodItemConfiguration : IEntityTypeConfiguration<FoodItem>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public void Configure(EntityTypeBuilder<FoodItem> builder)
    {
        builder.ToTable("food_items", table => table.HasCheckConstraint("CK_food_serving", "\"ServingSize\" > 0"));
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.UserId, x.Category });
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Category).HasMaxLength(50).IsRequired();
        builder.Property(x => x.ServingUnit).HasMaxLength(30).IsRequired();
        builder.Property(x => x.ServingSize).HasPrecision(12, 3);
        builder.Property(x => x.Allergens).HasColumnType("text[]").IsRequired(false);
        builder.Property(x => x.Micronutrients)
            .HasColumnName("Micronutrients")
            .HasColumnType("jsonb")
            .HasConversion(
                value => value == null ? null : JsonSerializer.Serialize(value, JsonOptions),
                value => string.IsNullOrWhiteSpace(value)
                    ? null
                    : JsonSerializer.Deserialize<Dictionary<string, decimal>>(value, JsonOptions));
        builder.Property(x => x.IsDeleted).HasDefaultValue(false);
        builder.HasQueryFilter(x => !x.IsDeleted);
        builder.OwnsOne(x => x.Nutrition, NutritionMapping.Configure);
        builder.Navigation(x => x.Nutrition).IsRequired();
    }
}
