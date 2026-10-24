namespace Diet.Application.Features.Meals;

/// <summary>Lượng ăn theo cùng đơn vị khẩu phần của món.</summary>
public sealed record LogMealItemRequest(Guid FoodId, decimal Quantity);
