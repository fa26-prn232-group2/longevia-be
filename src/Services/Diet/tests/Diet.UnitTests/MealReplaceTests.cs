using Diet.Application.Abstractions;
using Diet.Application.Features.Meals;
using Diet.Domain;
using Diet.Domain.Entities;
using Longevia.Shared.Auth;
using Longevia.Shared.Pagination;

namespace Diet.UnitTests;

public sealed class MealReplaceTests
{
    private static readonly DateOnly Day = new(2026, 11, 13);
    private static readonly Guid FoodId = Guid.NewGuid();

    [Fact]
    public async Task Replace_RemovesOldMealsAndPublishesRemovalThenLogged()
    {
        var userId = Guid.NewGuid();
        var food = Food();
        var oldMeal = MealLog.Create(userId, Day, "Breakfast", DateTimeOffset.UtcNow.AddHours(-2));
        oldMeal.Add(food, 100);

        var meals = new FakeMealRepository([oldMeal]);
        var activity = new FakeActivityPublisher();
        var useCases = CreateUseCases(userId, meals, activity, food);

        var result = await useCases.ReplaceAsync(Day, "Breakfast",
            [new LogMealItemRequest(food.Id, 250)], CancellationToken.None);

        Assert.Equal("Breakfast", result.MealType);
        Assert.Equal(250m, result.Items.Single().Quantity);
        Assert.Single(meals.Stored);
        Assert.NotEqual(oldMeal.Id, meals.Stored[0].Id);
        Assert.Contains(activity.Removed, meal => meal.Id == oldMeal.Id);
        Assert.Contains(activity.Published, meal => meal.Id == meals.Stored[0].Id);
        // Removal phải được phát trước logged để Progress xử lý đúng thứ tự.
        Assert.Single(activity.Removed);
        Assert.Single(activity.Published);
    }

    [Fact]
    public async Task Replace_WithoutExistingMeal_CreatesAndPublishesOnlyLogged()
    {
        var userId = Guid.NewGuid();
        var food = Food();
        var meals = new FakeMealRepository([]);
        var activity = new FakeActivityPublisher();
        var useCases = CreateUseCases(userId, meals, activity, food);

        var result = await useCases.ReplaceAsync(Day, "Dinner",
            [new LogMealItemRequest(food.Id, 120)], CancellationToken.None);

        Assert.Single(meals.Stored);
        Assert.Empty(activity.Removed);
        Assert.Single(activity.Published);
        Assert.Equal(result.Id, activity.Published[0].Id);
    }

    [Fact]
    public async Task Replace_InvalidMealType_ThrowsBadRequest()
    {
        var food = Food();
        var useCases = CreateUseCases(Guid.NewGuid(), new FakeMealRepository([]),
            new FakeActivityPublisher(), food);

        var error = await Assert.ThrowsAsync<Longevia.Shared.Errors.AppException>(() =>
            useCases.ReplaceAsync(Day, "Brunch", [new LogMealItemRequest(food.Id, 100)],
                CancellationToken.None));

        Assert.Equal(400, error.Status);
        Assert.Equal("diet.invalid_meal_type", error.Code);
    }

    private static MealUseCases CreateUseCases(Guid userId, FakeMealRepository meals,
        FakeActivityPublisher activity, FoodItem food) =>
        new(new FakeFoodRepository(food), meals, new FakeUnitOfWork(), new FakeCurrentUser(userId),
            TimeProvider.System, activity);

    private static FoodItem Food() =>
        FoodItem.Create(null, "Cơm trắng", "Grains", 100, "g",
            new NutritionValues(130, 2.7m, 28, 0.3m, 0.4m));

    private sealed class FakeCurrentUser(Guid userId) : ICurrentUser
    {
        public Guid? UserId => userId;
        public string? Email => "tester@example.com";
        public bool IsAuthenticated => true;
        public bool InRole(string role) => false;
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken ct) => Task.FromResult(0);

        public async Task ExecuteInTransactionAsync(string lockKey,
            Func<CancellationToken, Task> action, CancellationToken ct) => await action(ct);
    }

    private sealed class FakeFoodRepository(FoodItem food) : IFoodRepository
    {
        public Task<PagedResult<FoodItem>> SearchAsync(Guid userId, string? search, string? category,
            PaginationQuery pagination, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<FoodItem>> GetVisibleAsync(Guid userId,
            IReadOnlyCollection<Guid> ids, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<FoodItem>>(
                ids.Contains(food.Id) ? [food] : []);

        public Task<IReadOnlyList<FoodItem>> GetByIdsAsync(IReadOnlyCollection<Guid> ids,
            CancellationToken ct) => Task.FromResult<IReadOnlyList<FoodItem>>([food]);

        public Task<IReadOnlyList<FoodItem>> GetRecommendationCandidatesAsync(Guid userId,
            IReadOnlyList<string> allergies, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<FoodItem>>([food]);

        public void Add(FoodItem item) { }
    }

    private sealed class FakeMealRepository(IReadOnlyList<MealLog> existing) : IMealRepository
    {
        public List<MealLog> Stored { get; } = existing.ToList();

        public void Add(MealLog meal) => Stored.Add(meal);

        public Task<IReadOnlyList<MealLog>> GetDailyAsync(Guid userId, DateOnly date,
            CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<MealLog>>(
                Stored.Where(x => x.UserId == userId && x.Date == date).ToList());

        public Task<IReadOnlyList<MealLog>> GetDailyTrackedAsync(Guid userId, DateOnly date,
            CancellationToken ct) => GetDailyAsync(userId, date, ct);

        public Task<IReadOnlyList<MealLog>> GetRangeAsync(Guid userId, DateOnly from, DateOnly to,
            CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<MealLog>>(
                Stored.Where(x => x.UserId == userId && x.Date >= from && x.Date <= to).ToList());

        public void RemoveRange(IEnumerable<MealLog> meals)
        {
            foreach (var meal in meals.ToList())
            {
                Stored.Remove(meal);
            }
        }
    }

    private sealed class FakeActivityPublisher : IMealActivityPublisher
    {
        public List<MealLog> Published { get; } = [];
        public List<MealLog> Removed { get; } = [];

        public Task PublishAsync(MealLog meal, CancellationToken ct)
        {
            Published.Add(meal);
            return Task.CompletedTask;
        }

        public Task PublishRemovedAsync(MealLog meal, CancellationToken ct)
        {
            Removed.Add(meal);
            return Task.CompletedTask;
        }
    }
}
