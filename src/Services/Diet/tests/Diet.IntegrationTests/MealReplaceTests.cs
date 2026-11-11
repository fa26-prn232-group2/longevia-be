using System.Net;
using System.Net.Http.Json;
using Diet.Application.Features.Meals;
using Diet.Application.Features.Targets;
using Diet.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Diet.IntegrationTests;

public sealed class MealReplaceTests(DietApiFactory factory) : IClassFixture<DietApiFactory>
{
    private static readonly DateOnly Day = new(2026, 11, 13);
    private static readonly UpdateTargetRequest Target = new(2000, 125, 250, 55);
    private static readonly Guid Chicken = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid Rice = Guid.Parse("22222222-2222-4222-8222-222222222222");

    [Fact]
    public async Task Replace_SwapsItemsAndLeavesOtherMealsUntouched()
    {
        var userId = Guid.NewGuid();
        using var client = factory.UserClient(userId);
        await client.PutAsJsonAsync("/targets/me", Target);

        await client.PostAsJsonAsync("/meals",
            new LogMealRequest(Day, "Breakfast", [new(Chicken, 100), new(Rice, 100)]));
        await client.PostAsJsonAsync("/meals",
            new LogMealRequest(Day, "Lunch", [new(Chicken, 200)]));

        var replace = await client.PutAsJsonAsync(
            $"/meals?date={Day:yyyy-MM-dd}&mealType=Breakfast",
            new ReplaceMealRequest([new(Rice, 200)]));

        Assert.Equal(HttpStatusCode.OK, replace.StatusCode);
        var replaced = await replace.Content.ReadFromJsonAsync<MealResponse>();
        Assert.NotNull(replaced);
        Assert.Equal("Breakfast", replaced.MealType);
        Assert.Single(replaced.Items);
        Assert.Equal(260m, replaced.TotalCalories);

        var summary = await client.GetFromJsonAsync<DailySummaryResponse>(
            $"/meals/daily-summary?date={Day:yyyy-MM-dd}");
        Assert.NotNull(summary);
        Assert.Equal(2, summary.Meals.Count);
        Assert.Equal(1, summary.Meals.Single(x => x.MealType == "Breakfast").ItemsCount);
        Assert.Equal(330m, summary.Meals.Single(x => x.MealType == "Lunch").TotalCalories);
        Assert.Equal(590m, summary.ConsumedCalories);
    }

    [Fact]
    public async Task Replace_WithNoExistingMeal_CreatesOne()
    {
        var userId = Guid.NewGuid();
        var day = new DateOnly(2026, 11, 14);
        using var client = factory.UserClient(userId);
        await client.PutAsJsonAsync("/targets/me", Target);

        var replace = await client.PutAsJsonAsync(
            $"/meals?date={day:yyyy-MM-dd}&mealType=Dinner",
            new ReplaceMealRequest([new(Chicken, 150)]));

        Assert.Equal(HttpStatusCode.OK, replace.StatusCode);
        var summary = await client.GetFromJsonAsync<DailySummaryResponse>(
            $"/meals/daily-summary?date={day:yyyy-MM-dd}");
        Assert.Single(summary!.Meals);
        Assert.Equal("Dinner", summary.Meals.Single().MealType);
    }

    [Fact]
    public async Task Replace_UnknownFood_Returns404()
    {
        using var client = factory.UserClient(Guid.NewGuid());

        var replace = await client.PutAsJsonAsync(
            $"/meals?date={Day:yyyy-MM-dd}&mealType=Snack",
            new ReplaceMealRequest([new(Guid.NewGuid(), 100)]));

        Assert.Equal(HttpStatusCode.NotFound, replace.StatusCode);
        Assert.Contains("diet.food_not_found", await replace.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Replace_InvalidMealType_Returns400()
    {
        using var client = factory.UserClient(Guid.NewGuid());

        var replace = await client.PutAsJsonAsync(
            $"/meals?date={Day:yyyy-MM-dd}&mealType=Brunch",
            new ReplaceMealRequest([new(Rice, 100)]));

        Assert.Equal(HttpStatusCode.BadRequest, replace.StatusCode);
        Assert.Contains("diet.invalid_meal_type", await replace.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Replace_ConcurrentRequests_LeaveExactlyOneMealOfThatType()
    {
        var userId = Guid.NewGuid();
        var day = new DateOnly(2026, 11, 15);
        using var setup = factory.UserClient(userId);
        await setup.PutAsJsonAsync("/targets/me", Target);
        await setup.PostAsJsonAsync("/meals",
            new LogMealRequest(day, "Lunch", [new(Chicken, 100)]));

        var responses = await Task.WhenAll(Enumerable.Range(0, 5).Select(async index =>
        {
            using var client = factory.UserClient(userId);
            return await client.PutAsJsonAsync(
                $"/meals?date={day:yyyy-MM-dd}&mealType=Lunch",
                new ReplaceMealRequest([new(Rice, 100 + index * 10)]));
        }));

        Assert.All(responses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<DietDbContext>();
        Assert.Equal(1, await db.MealLogs.CountAsync(x =>
            x.UserId == userId && x.Date == day && x.MealType == "Lunch"));
        var log = await db.MealLogs.Include(x => x.Items)
            .SingleAsync(x => x.UserId == userId && x.Date == day && x.MealType == "Lunch");
        Assert.Single(log.Items);
    }
}
