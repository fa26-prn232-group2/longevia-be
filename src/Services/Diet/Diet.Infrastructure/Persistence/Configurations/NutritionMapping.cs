using Diet.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diet.Infrastructure.Persistence.Configurations;

internal static class NutritionMapping
{
    internal static void Configure<T>(OwnedNavigationBuilder<T, NutritionValues> builder) where T : class
    {
        builder.Property(x => x.Calories).HasColumnName("Calories").HasPrecision(28, 8);
        builder.Property(x => x.ProteinGrams).HasColumnName("ProteinGrams").HasPrecision(28, 8);
        builder.Property(x => x.CarbsGrams).HasColumnName("CarbsGrams").HasPrecision(28, 8);
        builder.Property(x => x.FatGrams).HasColumnName("FatGrams").HasPrecision(28, 8);
        builder.Property(x => x.FiberGrams).HasColumnName("FiberGrams").HasPrecision(28, 8);
    }
}
