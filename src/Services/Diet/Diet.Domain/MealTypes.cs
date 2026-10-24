namespace Diet.Domain;

public static class MealTypes
{
    public const string Breakfast = "Breakfast";
    public const string Lunch = "Lunch";
    public const string Dinner = "Dinner";
    public const string Snack = "Snack";
    public static IReadOnlyList<string> All { get; } = [Breakfast, Lunch, Dinner, Snack];
}
