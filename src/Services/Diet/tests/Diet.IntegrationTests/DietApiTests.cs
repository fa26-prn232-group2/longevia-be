using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Diet.Application.Features.Foods;
using Diet.Application.Features.Meals;
using Diet.Application.Features.Targets;
using Diet.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Diet.IntegrationTests;

public sealed class DietApiTests(DietApiFactory factory) : IClassFixture<DietApiFactory>
{
    private static readonly DateOnly Day = new(2026, 10, 24);
    private static readonly UpdateTargetRequest Target = new(2000, 125, 250, 55);
    private static readonly Guid Chicken = Guid.Parse("11111111-1111-4111-8111-111111111111");

    [Fact]
    public async Task Catalog_CustomFood_IsolatedAcrossSearchAndMealLogging()
    {
        using var owner = factory.UserClient(Guid.NewGuid());
        using var other = factory.UserClient(Guid.NewGuid());
        var created = await CreateFood(owner);
        var mine = await owner.GetFromJsonAsync<FoodPageResponse>("/foods?search=private");
        var theirs = await other.GetFromJsonAsync<FoodPageResponse>("/foods?search=private");
        Assert.Contains(mine!.Items, x => x.Id == created.Id);
        Assert.DoesNotContain(theirs!.Items, x => x.Id == created.Id);
        var response = await other.PostAsJsonAsync("/meals",
            new LogMealRequest(Day, "Lunch", [new(created.Id, 100)]));
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("diet.food_not_found", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Catalog_SearchFilterAndPaging_ReportsFullCountBeforeSkip()
    {
        using var client = factory.UserClient(Guid.NewGuid());
        await CreateFood(client);
        await CreateFood(client);
        var first = await client.GetFromJsonAsync<FoodPageResponse>("/foods?search=PRIVATE&category=Meat&pageSize=1");
        var second = await client.GetFromJsonAsync<FoodPageResponse>("/foods?search=private&category=Meat&pageSize=1&pageNumber=2");
        Assert.Equal(2, first!.TotalCount);
        Assert.Equal(2, first.TotalPages);
        Assert.True(first.HasNextPage);
        Assert.True(second!.HasPreviousPage);
        Assert.NotEqual(first.Items[0].Id, second.Items[0].Id);
    }

    [Fact]
    public async Task Targets_RepeatedAndConcurrentPut_KeepOneStableIdAndPrivateOwner()
    {
        var userId = Guid.NewGuid();
        using var client = factory.UserClient(userId);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/targets/me")).StatusCode);
        var responses = await Task.WhenAll(Enumerable.Range(0, 5)
            .Select(_ => client.PutAsJsonAsync("/targets/me", Target)));
        Assert.All(responses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
        var targets = await Task.WhenAll(responses.Select(x => x.Content.ReadFromJsonAsync<TargetResponse>()));
        Assert.Single(targets.Select(x => x!.Id).Distinct());
        Assert.Null(targets[0]!.Bmr);
        Assert.Null(targets[0]!.Tdee);
        var updated = await client.PutAsJsonAsync("/targets/me", new UpdateTargetRequest(1500, 100, 200, 30));
        var changed = await updated.Content.ReadFromJsonAsync<TargetResponse>();
        Assert.Equal(targets[0]!.Id, changed!.Id);
        Assert.Equal(1500, changed.TargetCalories);
        using var stranger = factory.UserClient(Guid.NewGuid());
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync("/targets/me")).StatusCode);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DietDbContext>();
        Assert.Equal(1, await db.DailyDietTargets.CountAsync(x => x.UserId == userId));
    }

    [Fact]
    public async Task Meals_MultipleLogs_SumScaledSnapshotsAndStayPrivate()
    {
        using var client = factory.UserClient(Guid.NewGuid());
        await client.PutAsJsonAsync("/targets/me", Target);
        for (var i = 0; i < 2; i++)
        {
            var response = await client.PostAsJsonAsync("/meals", new LogMealRequest(Day, "Lunch", [new(Chicken, 150)]));
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var meal = await response.Content.ReadFromJsonAsync<MealResponse>();
            Assert.Equal(247.5m, meal!.TotalCalories);
            Assert.Equal(46.5m, meal.Items.Single().ProteinGrams);
        }
        var summary = await client.GetFromJsonAsync<DailySummaryResponse>("/meals/daily-summary?date=2026-10-24");
        Assert.Equal(495m, summary!.ConsumedCalories);
        Assert.Equal(1505m, summary.RemainingCalories);
        Assert.Equal(93m, summary.Macros.Protein.Consumed);
        Assert.Equal(2, summary.Meals.Single().ItemsCount);
        using var stranger = factory.UserClient(Guid.NewGuid());
        await stranger.PutAsJsonAsync("/targets/me", Target);
        var empty = await stranger.GetFromJsonAsync<DailySummaryResponse>("/meals/daily-summary?date=2026-10-24");
        Assert.Equal(0, empty!.ConsumedCalories);
        Assert.Empty(empty.Meals);
    }

    [Fact]
    public async Task Meals_InvalidFoodInBatch_DoesNotWritePartialLog()
    {
        using var client = factory.UserClient(Guid.NewGuid());
        await client.PutAsJsonAsync("/targets/me", Target);
        var response = await client.PostAsJsonAsync("/meals",
            new LogMealRequest(Day, "Dinner", [new(Chicken, 100), new(Guid.NewGuid(), 100)]));
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var summary = await client.GetFromJsonAsync<DailySummaryResponse>("/meals/daily-summary?date=2026-10-24");
        Assert.Equal(0, summary!.ConsumedCalories);
        Assert.Empty(summary.Meals);
    }

    [Fact]
    public async Task Meals_CatalogChanges_DoNotRecalculateSavedSnapshot()
    {
        using var client = factory.UserClient(Guid.NewGuid());
        var food = await CreateFood(client);
        await client.PutAsJsonAsync("/targets/me", Target);
        var logged = await client.PostAsJsonAsync("/meals",
            new LogMealRequest(Day, "Dinner", [new(food.Id, 150)]));
        Assert.Equal(HttpStatusCode.Created, logged.StatusCode);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DietDbContext>();
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE food_items SET \"Calories\" = 999, \"Name\" = 'Changed' WHERE \"Id\" = {food.Id}");
        }
        var summary = await client.GetFromJsonAsync<DailySummaryResponse>("/meals/daily-summary?date=2026-10-24");
        Assert.Equal(247.5m, summary!.ConsumedCalories);
    }

    [Theory]
    [InlineData("GET", "/foods")]
    [InlineData("POST", "/foods")]
    [InlineData("GET", "/targets/me")]
    [InlineData("PUT", "/targets/me")]
    [InlineData("POST", "/meals")]
    [InlineData("GET", "/meals/daily-summary")]
    public async Task Endpoints_WithoutToken_ReturnUnauthorized(string method, string route)
    {
        using var client = factory.CreateClient();
        var response = await client.SendAsync(new HttpRequestMessage(new HttpMethod(method), route));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Validation_InvalidInput_ReturnsProblemDetailsAndDoesNotCreateFood()
    {
        using var client = factory.UserClient(Guid.NewGuid());
        var response = await client.PostAsJsonAsync("/foods",
            new CreateFoodRequest("private", "Meat", 0, "g", -1, 31, 0, 3.6m, 0));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("validation.failed", problem.RootElement.GetProperty("code").GetString());
        Assert.True(problem.RootElement.TryGetProperty("traceId", out _));
        Assert.True(problem.RootElement.TryGetProperty("errors", out _));
        var foods = await client.GetFromJsonAsync<FoodPageResponse>("/foods?search=private");
        Assert.Empty(foods!.Items);
    }

    [Fact]
    public async Task Validation_NullItemsAndInvalidMacroRatio_ReturnBadRequest()
    {
        using var client = factory.UserClient(Guid.NewGuid());
        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.PostAsJsonAsync("/meals", new { date = Day, mealType = "Lunch", items = (object?)null })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.PutAsJsonAsync("/targets/me", new UpdateTargetRequest(2000, 1, 1, 1))).StatusCode);
    }

    [Fact]
    public async Task Auth_SignedTokenWithInvalidSubject_ReturnsUnauthorized()
    {
        using var client = factory.UserClient(Guid.NewGuid(), "invalid-user");
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/foods")).StatusCode);
    }

    [Fact]
    public async Task ObservabilityAndSwagger_ExposeMetricsAndSixBusinessOperations()
    {
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health")).StatusCode);
        Assert.Contains("http_server", await client.GetStringAsync("/metrics"));
        using var swagger = JsonDocument.Parse(await client.GetStringAsync("/swagger/v1/swagger.json"));
        var paths = swagger.RootElement.GetProperty("paths");
        Assert.Equal(6, paths.EnumerateObject().Where(x => !x.Name.StartsWith("/health") &&
            !x.Name.StartsWith("/alive") && !x.Name.StartsWith("/metrics")).Sum(x => x.Value.EnumerateObject().Count()));
        var schemaNames = new[]
        {
            "CreateFoodRequest", "UpdateTargetRequest", "LogMealRequest", "LogMealItemRequest",
            "FoodResponse", "FoodPageResponse", "TargetResponse", "MealResponse", "MealItemResponse",
            "DailySummaryResponse", "MacroSummary", "MacroProgress", "MealSummary",
        };
        var schemas = swagger.RootElement.GetProperty("components").GetProperty("schemas");
        Assert.Equal(73, schemaNames.Sum(name => schemas.GetProperty(name).GetProperty("properties").EnumerateObject().Count()));
    }

    private static async Task<FoodResponse> CreateFood(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/foods",
            new CreateFoodRequest("private chicken", "Meat", 100, "g", 165, 31, 0, 3.6m, 0));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<FoodResponse>())!;
    }
}
