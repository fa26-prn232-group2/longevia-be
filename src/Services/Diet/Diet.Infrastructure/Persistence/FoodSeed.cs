using Diet.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Diet.Infrastructure.Persistence;

internal static class FoodSeed
{
    internal static void Configure(ModelBuilder model)
    {
        // Small illustrative local catalog, not a complete nutritional reference.
        Seed(model, Guid.Parse("11111111-1111-4111-8111-111111111111"),
            "Ức gà (demo)", "Meat", 100, "g", 165, 31, 0, 3.6m, 0);
        Seed(model, Guid.Parse("22222222-2222-4222-8222-222222222222"),
            "Cơm trắng (demo)", "Grains", 100, "g", 130, 2.7m, 28, 0.3m, 0.4m);
        Seed(model, Guid.Parse("33333333-3333-4333-8333-333333333333"),
            "Nước lọc (demo)", "Beverage", 100, "ml", 0, 0, 0, 0, 0);
    }

    private static void Seed(ModelBuilder model, Guid id, string name, string category,
        decimal servingSize, string servingUnit, decimal calories, decimal protein,
        decimal carbs, decimal fat, decimal fiber)
    {
        model.Entity<FoodItem>().HasData(new
        {
            Id = id,
            UserId = (Guid?)null,
            Name = name,
            Category = category,
            ServingSize = servingSize,
            ServingUnit = servingUnit,
        });
        model.Entity<FoodItem>().OwnsOne(x => x.Nutrition).HasData(new
        {
            FoodItemId = id,
            Calories = calories,
            ProteinGrams = protein,
            CarbsGrams = carbs,
            FatGrams = fat,
            FiberGrams = fiber,
        });
    }
}
