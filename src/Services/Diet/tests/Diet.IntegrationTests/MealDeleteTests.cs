using System.Net;
using System.Net.Http.Json;
using Diet.Application.Features.Meals;
using Diet.Application.Features.Targets;

namespace Diet.IntegrationTests;

public sealed class MealDeleteTests(DietApiFactory factory) : IClassFixture<DietApiFactory>
{
    private static readonly DateOnly Day = new(2026, 11, 11);
    private static readonly UpdateTargetRequest Target = new(2000, 125, 250, 55);
    private static readonly Guid Chicken = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid Rice = Guid.Parse("22222222-2222-4222-8222-222222222222");

    [Fact]
    public async Task Delete_RemovesOnlyThatMealTypeInTheDay_AndQueuesRemovalEvent()
    {
        using var client = factory.UserClient(Guid.NewGuid());
        await client.PutAsJsonAsync("/targets/me", Target);
        await Log(client, "Breakfast");
        await Log(client, "Lunch");

        var delete = await client.DeleteAsync($"/meals?date={Day:yyyy-MM-dd}&mealType=Breakfast");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        var summary = await client.GetFromJsonAsync<DailySummaryResponse>(
            $"/meals/daily-summary?date={Day:yyyy-MM-dd}");
        Assert.NotNull(summary);
        Assert.Single(summary.Meals);
        Assert.Equal("Lunch", summary.Meals.Single().MealType);
    }

    [Fact]
    public async Task Delete_IsIdempotent_AndValidatesMealType()
    {
        using var client = factory.UserClient(Guid.NewGuid());

        var missing = await client.DeleteAsync($"/meals?date={Day:yyyy-MM-dd}&mealType=Dinner");
        Assert.Equal(HttpStatusCode.NoContent, missing.StatusCode);

        var invalid = await client.DeleteAsync($"/meals?date={Day:yyyy-MM-dd}&mealType=SecondBreakfast");
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Contains("diet.invalid_meal_type", await invalid.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Delete_DoesNotTouchAnotherUsersMeal()
    {
        var day = new DateOnly(2026, 11, 12);
        using var owner = factory.UserClient(Guid.NewGuid());
        using var other = factory.UserClient(Guid.NewGuid());
        await owner.PutAsJsonAsync("/targets/me", Target);
        await other.PutAsJsonAsync("/targets/me", Target);
        await Log(owner, "Snack", day);

        var delete = await other.DeleteAsync($"/meals?date={day:yyyy-MM-dd}&mealType=Snack");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        var ownerSummary = await owner.GetFromJsonAsync<DailySummaryResponse>(
            $"/meals/daily-summary?date={day:yyyy-MM-dd}");
        Assert.Single(ownerSummary!.Meals);
    }

    private static async Task Log(HttpClient client, string mealType, DateOnly? date = null)
    {
        var response = await client.PostAsJsonAsync("/meals",
            new LogMealRequest(date ?? Day, mealType,
                [new(Chicken, 150), new(Rice, 100)]));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }
}
