using Diet.Application.Abstractions;
using Diet.Application.Common;
using Diet.Domain;
using Diet.Domain.Entities;
using Longevia.Shared.Auth;

namespace Diet.Application.Features.Meals;

public sealed class MealUseCases(IFoodRepository foods, IMealRepository meals,
    IUnitOfWork unitOfWork, ICurrentUser user, TimeProvider time, IMealActivityPublisher activity)
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
        await activity.PublishAsync(meal, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return MealResponse.From(meal);
    }

    /// <summary>
    /// Thay thế toàn bộ bữa ăn cùng loại trong ngày: xoá các log cũ (phát removal),
    /// tạo log mới (phát logged) trong cùng transaction để Progress giữ streak đúng.
    /// </summary>
    public async Task<MealResponse> ReplaceAsync(DateOnly? date, string mealType,
        IReadOnlyList<LogMealItemRequest> items, CancellationToken ct)
    {
        var canonicalType = MealTypes.All.FirstOrDefault(x =>
            x.Equals(mealType?.Trim(), StringComparison.OrdinalIgnoreCase));
        if (canonicalType is null)
        {
            throw DietErrors.InvalidMealType();
        }

        var userId = user.RequiredId();
        var day = date ?? Longevia.Shared.Time.BusinessTime.ToDate(time.GetUtcNow());
        var ids = items.Select(x => x.FoodId).Distinct().ToArray();
        var available = (await foods.GetVisibleAsync(userId, ids, ct)).ToDictionary(x => x.Id);
        if (ids.Any(id => !available.ContainsKey(id)))
        {
            throw DietErrors.MissingFood();
        }

        var meal = MealLog.Create(userId, day, canonicalType, time.GetUtcNow());
        foreach (var item in items)
        {
            meal.Add(available[item.FoodId], item.Quantity);
        }

        await unitOfWork.ExecuteInTransactionAsync(
            $"meal:{userId:N}:{day:yyyy-MM-dd}:{canonicalType}",
            async token =>
            {
                var existing = await meals.GetDailyTrackedAsync(userId, day, token);
                var targets = existing.Where(x => x.MealType == canonicalType).ToArray();
                foreach (var old in targets)
                {
                    await activity.PublishRemovedAsync(old, token);
                }

                if (targets.Length > 0)
                {
                    meals.RemoveRange(targets);
                }

                meals.Add(meal);
                await activity.PublishAsync(meal, token);
                await unitOfWork.SaveChangesAsync(token);
            },
            ct);

        return MealResponse.From(meal);
    }

    /// <summary>
    /// Xoá toàn bộ bữa ăn cùng loại trong một ngày (ví dụ cả "Bữa sáng" hôm nay).
    /// Idempotent: không còn bữa nào thì trả về bình thường để client không thấy lỗi
    /// khi bấm xoá hai lần.
    /// </summary>
    public async Task DeleteAsync(DateOnly? date, string mealType, CancellationToken ct)
    {
        var canonicalType = MealTypes.All.FirstOrDefault(x =>
            x.Equals(mealType?.Trim(), StringComparison.OrdinalIgnoreCase));
        if (canonicalType is null)
        {
            throw DietErrors.InvalidMealType();
        }

        var userId = user.RequiredId();
        var day = date ?? Longevia.Shared.Time.BusinessTime.ToDate(time.GetUtcNow());
        var logs = await meals.GetDailyTrackedAsync(userId, day, ct);
        var targets = logs.Where(x => x.MealType == canonicalType).ToArray();
        if (targets.Length == 0)
        {
            return;
        }

        foreach (var meal in targets)
        {
            await activity.PublishRemovedAsync(meal, ct);
        }

        meals.RemoveRange(targets);
        await unitOfWork.SaveChangesAsync(ct);
    }
}
