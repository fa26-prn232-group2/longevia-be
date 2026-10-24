using Diet.Application.Abstractions;
using Diet.Application.Common;
using Diet.Domain;
using Diet.Domain.Entities;
using Longevia.Shared.Auth;

namespace Diet.Application.Features.Meals;

public sealed class MealUseCases(IFoodRepository foods, IMealRepository meals,
    IUnitOfWork unitOfWork, ICurrentUser user, TimeProvider time)
{
    public async Task<MealResponse> LogAsync(LogMealRequest request, CancellationToken ct)
    {
        var userId = user.RequiredId();
        var ids = request.Items.Select(x => x.FoodId).Distinct().ToArray();
        var available = (await foods.GetVisibleAsync(userId, ids, ct)).ToDictionary(x => x.Id);
        if (ids.Any(id => !available.ContainsKey(id)))
        {
            throw DietErrors.MissingFood();
        }

        var meal = MealLog.Create(userId, request.Date, request.MealType, time.GetUtcNow());
        foreach (var item in request.Items)
        {
            meal.Add(available[item.FoodId], item.Quantity);
        }

        meals.Add(meal);
        await unitOfWork.SaveChangesAsync(ct);
        return MealResponse.From(meal);
    }
}
