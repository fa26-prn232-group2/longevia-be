namespace Diet.Application.Features.Foods;

/// <summary>Thực phẩm tùy chỉnh của người dùng hiện tại, dinh dưỡng theo một khẩu phần.</summary>
public sealed record CreateFoodRequest(
    string Name, string Category, decimal ServingSize, string ServingUnit,
    decimal Calories, decimal ProteinGrams, decimal CarbsGrams, decimal FatGrams, decimal FiberGrams);
