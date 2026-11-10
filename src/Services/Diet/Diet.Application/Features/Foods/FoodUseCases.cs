using Diet.Application.Abstractions;
using Diet.Application.Common;
using Diet.Domain;
using Diet.Domain.Entities;
using Longevia.Shared.Auth;
using Longevia.Shared.Pagination;

namespace Diet.Application.Features.Foods;

public sealed class FoodUseCases(IFoodRepository foods, IUnitOfWork unitOfWork, ICurrentUser user)
{
    public async Task<FoodResponse> CreateAsync(CreateFoodRequest request, CancellationToken ct)
    {
        var food = FoodItem.Create(user.RequiredId(), request.Name, request.Category,
            request.ServingSize, request.ServingUnit, new NutritionValues(request.Calories,
                request.ProteinGrams, request.CarbsGrams, request.FatGrams, request.FiberGrams), request.Allergens);
        foods.Add(food);
        await unitOfWork.SaveChangesAsync(ct);
        return FoodResponse.From(food);
    }

    public async Task<FoodPageResponse> SearchAsync(string? search, string? category,
        PaginationQuery pagination, CancellationToken ct)
    {
        var page = await foods.SearchAsync(user.RequiredId(), search, category, pagination, ct);
        return new(page.Items.Select(FoodResponse.From).ToList(), page.Page, page.PageSize,
            page.TotalItems, page.TotalPages, page.HasPreviousPage, page.HasNextPage);
    }

    public async Task<FoodResponse> UpdateAsync(Guid id, CreateFoodRequest request, CancellationToken ct)
    {
        var food = await GetOwnedAsync(id, ct);
        food.Update(request.Name, request.Category, request.ServingSize, request.ServingUnit,
            new NutritionValues(request.Calories, request.ProteinGrams, request.CarbsGrams,
                request.FatGrams, request.FiberGrams), request.Allergens);
        await unitOfWork.SaveChangesAsync(ct);
        return FoodResponse.From(food);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var food = await GetOwnedAsync(id, ct);
        food.SoftDelete();
        await unitOfWork.SaveChangesAsync(ct);
    }

    private async Task<FoodItem> GetOwnedAsync(Guid id, CancellationToken ct)
    {
        var userId = user.RequiredId();
        var food = (await foods.GetVisibleAsync(userId, [id], ct)).SingleOrDefault();
        if (food is null) throw DietErrors.MissingFood();
        if (food.UserId != userId)
            throw Longevia.Shared.Errors.AppException.BadRequest(
                "Chỉ có thể sửa/xoá món do bạn tạo.", "diet.food_not_owned");
        return food;
    }
}
