using System.Net;
using System.Net.Http.Json;
using Diet.Application.Features.Meals;
using Diet.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Diet.IntegrationTests;

public sealed class UtcBusinessDateTests
{
    [Fact]
    public async Task UtcToday_RollsAtUtcMidnight_ExplicitHistoricalMealRemainsUnchanged()
    {
        var clock = new Clock { Now = DateTimeOffset.Parse("2026-11-04T23:59:59Z") };
        await using var factory = new DietApiFactory { Clock = clock };
        await factory.InitializeAsync();
        var user = Guid.NewGuid();
        using var client = factory.UserClient(user);
        using var target = await client.PutAsJsonAsync("/targets/me", new
        { targetCalories = 2000, targetProteinGrams = 100, targetCarbsGrams = 250, targetFatGrams = 60 });
        Assert.Equal(HttpStatusCode.OK, target.StatusCode);
        var selected = new DateOnly(2026, 10, 1);
        using var logged = await client.PostAsJsonAsync("/meals", new LogMealRequest(selected, "Lunch",
            [new(Guid.Parse("11111111-1111-4111-8111-111111111111"), 150)]));
        Assert.Equal(HttpStatusCode.Created, logged.StatusCode);
        var meal = (await logged.Content.ReadFromJsonAsync<MealResponse>())!;
        Assert.Equal(selected, meal.Date);
        var first = await client.GetFromJsonAsync<DailySummaryResponse>("/meals/daily-summary");
        Assert.Equal(new DateOnly(2026, 11, 4), first!.Date);
        clock.Now = DateTimeOffset.Parse("2026-11-05T00:00:00Z");
        var next = await client.GetFromJsonAsync<DailySummaryResponse>("/meals/daily-summary");
        Assert.Equal(new DateOnly(2026, 11, 5), next!.Date);
        var historical = await client.GetFromJsonAsync<DailySummaryResponse>("/meals/daily-summary?date=2026-10-01");
        Assert.Equal(selected, historical!.Date);
        Assert.True(historical.ConsumedCalories > 0);
        using var scope = factory.Services.CreateScope();
        var persisted = await scope.ServiceProvider.GetRequiredService<DietDbContext>().MealLogs.AsNoTracking().SingleAsync(x => x.Id == meal.Id);
        Assert.Equal(selected, persisted.Date);
        Assert.Equal(DateTimeOffset.Parse("2026-11-04T23:59:59Z"), persisted.CreatedAt);
    }

    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now { get; set; }
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
