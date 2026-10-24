namespace Diet.Domain.Entities;

public sealed class MealLogItem
{
    public Guid Id { get; private set; }
    public Guid MealLogId { get; private set; }
    public Guid FoodId { get; private set; }
    public string Name { get; private set; } = "";
    public string ServingUnit { get; private set; } = "";
    public decimal Quantity { get; private set; }
    public NutritionValues Nutrition { get; private set; } = new(0, 0, 0, 0, 0);

    private MealLogItem() { }

    public static MealLogItem Create(Guid mealLogId, FoodItem food, decimal quantity) => new()
    {
        Id = Guid.NewGuid(),
        MealLogId = mealLogId,
        FoodId = food.Id,
        Name = food.Name,
        ServingUnit = food.ServingUnit,
        Quantity = quantity,
        Nutrition = food.Nutrition.ForQuantity(quantity, food.ServingSize),
    };
}
