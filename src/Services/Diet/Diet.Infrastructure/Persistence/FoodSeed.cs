using Diet.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Diet.Infrastructure.Persistence;

internal static class FoodSeed
{
    // Allergen tags are matched case-insensitively against the free-text allergies in the
    // user's Identity profile, so each allergen group lists the English tag plus common
    // Vietnamese words people type (e.g. "peanut", "đậu phộng", "lạc").
    // An empty array means "declared: no common allergen"; null means unknown and the food
    // is never offered as an AI recommendation candidate.
    private static readonly string[] None = [];
    private static readonly string[] Egg = ["egg", "trứng"];
    private static readonly string[] Milk = ["milk", "sữa", "lactose"];
    private static readonly string[] Fish = ["fish", "cá", "hải sản"];
    private static readonly string[] Shellfish = ["shellfish", "tôm", "giáp xác", "hải sản"];
    private static readonly string[] Mollusc = ["mollusc", "mực", "nhuyễn thể", "hải sản"];
    private static readonly string[] Soy = ["soy", "đậu nành"];
    private static readonly string[] Gluten = ["gluten", "wheat", "lúa mì"];
    private static readonly string[] Peanut = ["peanut", "đậu phộng", "lạc"];
    private static readonly string[] Sesame = ["sesame", "mè", "vừng"];

    private static string[] TreeNut(string vietnameseName) => ["tree_nut", "hạt cây", vietnameseName];

    internal static void Configure(ModelBuilder model)
    {
        // Illustrative local catalog with approximate values from public nutrition references
        // (e.g. USDA FoodData Central). Not a complete or clinical nutritional reference.
        // Nutrition values are per ServingSize ServingUnit.

        // Original demo items (ids are stable; allergens declared so they can be recommended).
        Seed(model, "11111111-1111-4111-8111-111111111111",
            "Ức gà", "Meat", 100, "g", 165, 31, 0, 3.6m, 0, None);
        Seed(model, "22222222-2222-4222-8222-222222222222",
            "Cơm trắng", "Grains", 100, "g", 130, 2.7m, 28, 0.3m, 0.4m, None);
        Seed(model, "33333333-3333-4333-8333-333333333333",
            "Nước lọc", "Beverage", 100, "ml", 0, 0, 0, 0, 0, None);

        // Grains & starches
        Seed(model, "d1e70000-0000-4000-8000-000000000001",
            "Cơm gạo lứt", "Grains", 100, "g", 112, 2.3m, 23.5m, 0.8m, 1.8m, None);
        Seed(model, "d1e70000-0000-4000-8000-000000000002",
            "Yến mạch cán", "Grains", 40, "g", 156, 6.8m, 26.5m, 2.8m, 4.2m, Gluten);
        Seed(model, "d1e70000-0000-4000-8000-000000000003",
            "Bánh mì nguyên cám", "Grains", 100, "g", 247, 13, 41, 3.4m, 7, Gluten);
        Seed(model, "d1e70000-0000-4000-8000-000000000004",
            "Bún tươi", "Grains", 100, "g", 109, 0.9m, 25, 0.2m, 1, None);
        Seed(model, "d1e70000-0000-4000-8000-000000000005",
            "Bánh phở tươi", "Grains", 100, "g", 141, 1.8m, 32, 0.3m, 0.6m, None);
        Seed(model, "d1e70000-0000-4000-8000-000000000006",
            "Khoai lang luộc", "Grains", 100, "g", 86, 1.6m, 20.1m, 0.1m, 3, None);
        Seed(model, "d1e70000-0000-4000-8000-000000000007",
            "Ngô luộc", "Grains", 100, "g", 96, 3.4m, 21, 1.5m, 2.4m, None);

        // Meat
        Seed(model, "d1e70000-0000-4000-8000-000000000011",
            "Thịt heo nạc", "Meat", 100, "g", 143, 26, 0, 4.3m, 0, None);
        Seed(model, "d1e70000-0000-4000-8000-000000000012",
            "Thịt bò nạc", "Meat", 100, "g", 150, 21, 0, 7, 0, None);
        Seed(model, "d1e70000-0000-4000-8000-000000000013",
            "Đùi gà bỏ da", "Meat", 100, "g", 121, 19.7m, 0, 4.1m, 0, None);

        // Seafood
        Seed(model, "d1e70000-0000-4000-8000-000000000021",
            "Cá hồi", "Seafood", 100, "g", 208, 20, 0, 13, 0, Fish);
        Seed(model, "d1e70000-0000-4000-8000-000000000022",
            "Cá thu", "Seafood", 100, "g", 205, 19, 0, 13.9m, 0, Fish);
        Seed(model, "d1e70000-0000-4000-8000-000000000023",
            "Cá basa", "Seafood", 100, "g", 90, 15, 0, 3.2m, 0, Fish);
        Seed(model, "d1e70000-0000-4000-8000-000000000024",
            "Tôm", "Seafood", 100, "g", 99, 24, 0.2m, 0.3m, 0, Shellfish);
        Seed(model, "d1e70000-0000-4000-8000-000000000025",
            "Mực", "Seafood", 100, "g", 92, 15.6m, 3.1m, 1.4m, 0, Mollusc);

        // Eggs, soy & legumes
        Seed(model, "d1e70000-0000-4000-8000-000000000031",
            "Trứng gà luộc", "Eggs", 50, "g", 78, 6.3m, 0.6m, 5.3m, 0, Egg);
        Seed(model, "d1e70000-0000-4000-8000-000000000032",
            "Đậu phụ", "Legumes", 100, "g", 76, 8, 1.9m, 4.8m, 0.3m, Soy);
        Seed(model, "d1e70000-0000-4000-8000-000000000033",
            "Đậu xanh nấu chín", "Legumes", 100, "g", 105, 7, 19, 0.4m, 7.6m, None);
        Seed(model, "d1e70000-0000-4000-8000-000000000034",
            "Đậu đen nấu chín", "Legumes", 100, "g", 132, 8.9m, 23.7m, 0.5m, 8.7m, None);

        // Dairy & drinks
        Seed(model, "d1e70000-0000-4000-8000-000000000041",
            "Sữa tươi không đường", "Dairy", 100, "ml", 61, 3.2m, 4.8m, 3.3m, 0, Milk);
        Seed(model, "d1e70000-0000-4000-8000-000000000042",
            "Sữa chua không đường", "Dairy", 100, "g", 61, 3.5m, 4.7m, 3.3m, 0, Milk);
        Seed(model, "d1e70000-0000-4000-8000-000000000043",
            "Sữa đậu nành không đường", "Beverage", 100, "ml", 33, 2.9m, 1.7m, 1.6m, 0.4m, Soy);
        Seed(model, "d1e70000-0000-4000-8000-000000000044",
            "Trà xanh không đường", "Beverage", 100, "ml", 1, 0, 0.2m, 0, 0, None);

        // Vegetables
        Seed(model, "d1e70000-0000-4000-8000-000000000051",
            "Rau muống luộc", "Vegetables", 100, "g", 19, 2.6m, 3.1m, 0.2m, 2.1m, None);
        Seed(model, "d1e70000-0000-4000-8000-000000000052",
            "Cải bó xôi", "Vegetables", 100, "g", 23, 2.9m, 3.6m, 0.4m, 2.2m, None);
        Seed(model, "d1e70000-0000-4000-8000-000000000053",
            "Bông cải xanh", "Vegetables", 100, "g", 34, 2.8m, 6.6m, 0.4m, 2.6m, None);
        Seed(model, "d1e70000-0000-4000-8000-000000000054",
            "Cà chua", "Vegetables", 100, "g", 18, 0.9m, 3.9m, 0.2m, 1.2m, None);
        Seed(model, "d1e70000-0000-4000-8000-000000000055",
            "Cà rốt", "Vegetables", 100, "g", 41, 0.9m, 9.6m, 0.2m, 2.8m, None);
        Seed(model, "d1e70000-0000-4000-8000-000000000056",
            "Bí đỏ", "Vegetables", 100, "g", 26, 1, 6.5m, 0.1m, 0.5m, None);
        Seed(model, "d1e70000-0000-4000-8000-000000000057",
            "Nấm rơm", "Vegetables", 100, "g", 32, 3.8m, 4.6m, 0.7m, 2.5m, None);

        // Fruits
        Seed(model, "d1e70000-0000-4000-8000-000000000061",
            "Chuối", "Fruits", 100, "g", 89, 1.1m, 22.8m, 0.3m, 2.6m, None);
        Seed(model, "d1e70000-0000-4000-8000-000000000062",
            "Cam", "Fruits", 100, "g", 47, 0.9m, 11.8m, 0.1m, 2.4m, None);
        Seed(model, "d1e70000-0000-4000-8000-000000000063",
            "Táo", "Fruits", 100, "g", 52, 0.3m, 13.8m, 0.2m, 2.4m, None);
        Seed(model, "d1e70000-0000-4000-8000-000000000064",
            "Bơ", "Fruits", 100, "g", 160, 2, 8.5m, 14.7m, 6.7m, None);
        Seed(model, "d1e70000-0000-4000-8000-000000000065",
            "Đu đủ", "Fruits", 100, "g", 43, 0.5m, 10.8m, 0.3m, 1.7m, None);

        // Nuts & seeds (small servings)
        Seed(model, "d1e70000-0000-4000-8000-000000000071",
            "Hạt điều", "Nuts", 30, "g", 166, 5.5m, 9, 13.2m, 1, TreeNut("hạt điều"));
        Seed(model, "d1e70000-0000-4000-8000-000000000072",
            "Hạnh nhân", "Nuts", 30, "g", 174, 6.4m, 6.5m, 15, 3.8m, TreeNut("hạnh nhân"));
        Seed(model, "d1e70000-0000-4000-8000-000000000073",
            "Óc chó", "Nuts", 30, "g", 196, 4.6m, 4.1m, 19.6m, 2, TreeNut("óc chó"));
        Seed(model, "d1e70000-0000-4000-8000-000000000074",
            "Đậu phộng rang", "Nuts", 30, "g", 176, 7.1m, 6.5m, 14.9m, 2.6m, Peanut);
        Seed(model, "d1e70000-0000-4000-8000-000000000075",
            "Mè đen", "Nuts", 10, "g", 57, 1.8m, 2.3m, 5, 1.2m, Sesame);
    }

    private static void Seed(ModelBuilder model, string id, string name, string category,
        decimal servingSize, string servingUnit, decimal calories, decimal protein,
        decimal carbs, decimal fat, decimal fiber, string[] allergens)
    {
        var foodId = Guid.Parse(id);
        model.Entity<FoodItem>().HasData(new
        {
            Id = foodId,
            UserId = (Guid?)null,
            Name = name,
            Category = category,
            ServingSize = servingSize,
            ServingUnit = servingUnit,
            // Stored normalized like FoodItem.Create: trimmed, lower-case, distinct, ordinal-sorted.
            Allergens = allergens.Select(x => x.Trim().ToLowerInvariant())
                .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(),
        });
        model.Entity<FoodItem>().OwnsOne(x => x.Nutrition).HasData(new
        {
            FoodItemId = foodId,
            Calories = calories,
            ProteinGrams = protein,
            CarbsGrams = carbs,
            FatGrams = fat,
            FiberGrams = fiber,
        });
    }
}
