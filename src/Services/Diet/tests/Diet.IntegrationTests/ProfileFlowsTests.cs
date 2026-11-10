using System.Net;
using System.Net.Http.Json;
using Diet.Application.Abstractions;
using Diet.Application.Features.Foods;
using Diet.Application.Features.Meals;
using Diet.Application.Features.Targets;
using Diet.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Diet.IntegrationTests;

public sealed class ProfileFlowsTests(DietApiFactory factory) : IClassFixture<DietApiFactory>
{
    [Fact]
    public async Task Calculate_UsesTrustedProfileAndWeight_PersistsSnapshot()
    {
        using var host = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.AddScoped<IProfileProvider, ProfileStub>();
            services.AddScoped<IWeightProvider, WeightStub>();
        }));
        var userId = Guid.NewGuid();
        using var client = factory.UserClient(userId);
        using var api = host.CreateClient();
        api.DefaultRequestHeaders.Authorization = client.DefaultRequestHeaders.Authorization;
        var response = await api.PostAsJsonAsync("/targets/me/calculate", new UpdateTargetRequest(2000, 125, 250, 55));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = (await response.Content.ReadFromJsonAsync<TargetResponse>())!;
        Assert.Equal(1648.75m, result.Bmr);
        Assert.Equal(2555.5625m, result.Tdee);
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DietDbContext>();
        var stored = await db.DailyDietTargets.AsNoTracking().SingleAsync(x => x.UserId == userId);
        Assert.Equal(WeightStub.Source, stored.Calculation!.SourceMetricId);
        Assert.Equal(70m, stored.Calculation.WeightKg);
        Assert.Equal(30, stored.Calculation.AgeYears);
        var second = (await api.GetFromJsonAsync<TargetResponse>("/targets/me"))!;
        Assert.Equal(result, second);
    }

    [Fact]
    public async Task Calculate_MissingWeight_DoesNotCreateTarget()
    {
        using var host = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.AddScoped<IProfileProvider, ProfileStub>();
            services.AddScoped<IWeightProvider, MissingWeight>();
        }));
        using var tokenClient = factory.UserClient(Guid.NewGuid());
        using var client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization = tokenClient.DefaultRequestHeaders.Authorization;
        var response = await client.PostAsJsonAsync("/targets/me/calculate", new UpdateTargetRequest(2000, 125, 250, 55));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Contains("diet.weight_not_found", await response.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/targets/me")).StatusCode);
    }

    [Fact]
    public async Task MealAi_UsesTodaySummaryAndPrivateCatalog_ReturnsCitations()
    {
        var capture = new RecommendationStub();
        using var host = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.AddScoped<IProfileProvider, ProfileStub>();
            services.AddScoped<IWeightProvider, WeightStub>();
            services.AddSingleton<IMealRecommendationProvider>(capture);
        }));
        using var tokenClient = factory.UserClient(Guid.NewGuid());
        using var client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization = tokenClient.DefaultRequestHeaders.Authorization;
        await client.PutAsJsonAsync("/targets/me", new UpdateTargetRequest(2000, 125, 250, 55));
        var foodResponse = await client.PostAsJsonAsync("/foods", new CreateFoodRequest(
            "Flow rice", "Grains", 100, "g", 130, 3, 28, 1, 1, []));
        var food = (await foodResponse.Content.ReadFromJsonAsync<FoodResponse>())!;
        await client.PostAsJsonAsync("/meals", new LogMealRequest(Longevia.Shared.Time.BusinessTime.ToDate(DateTimeOffset.UtcNow), "Lunch", [new(food.Id, 100)]));
        await client.PostAsJsonAsync("/foods", new CreateFoodRequest("Allergen rice", "Grains", 100, "g", 130, 3, 28, 1, 1, ["peanuts"]));
        using var foreignToken = factory.UserClient(Guid.NewGuid());
        using var foreign = host.CreateClient();
        foreign.DefaultRequestHeaders.Authorization = foreignToken.DefaultRequestHeaders.Authorization;
        await foreign.PostAsJsonAsync("/foods", new CreateFoodRequest("Foreign rice", "Grains", 100, "g", 130, 3, 28, 1, 1, []));
        var response = await client.GetAsync("/meals/ai-recommendations?mealType=Dinner&goal=Maintain");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("disclaimer", body);
        Assert.Contains("citations", body);
        Assert.Equal(130, capture.Input!.ConsumedCalories);
        Assert.Equal(122m, capture.Input.ProteinRemaining);
        Assert.Equal("None", capture.Input.Profile.DietaryPreference);
        Assert.Contains(capture.Input.Foods, x => x.Id == food.Id);
        Assert.All(capture.Input.Foods, x => Assert.NotNull(x.Allergens));
        Assert.DoesNotContain(capture.Input.Foods, x => x.Name == "Allergen rice" || x.Name == "Foreign rice");
        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.GetAsync("/meals/ai-recommendations?mealType=Invalid&goal=Maintain")).StatusCode);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task MealAi_IncompleteProfileOrExpiredBudget_ReturnsExplicitError(bool incomplete)
    {
        using var host = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.AddScoped<IProfileProvider>(_ => new MetadataProfile(incomplete));
            services.AddScoped<IWeightProvider, WeightStub>();
            services.AddScoped<IMealRecommendationProvider, SlowRecommendation>();
        }));
        using var tokenClient = factory.UserClient(Guid.NewGuid());
        using var client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization = tokenClient.DefaultRequestHeaders.Authorization;
        await client.PutAsJsonAsync("/targets/me", new UpdateTargetRequest(2000, 125, 250, 55));
        await client.PostAsJsonAsync("/foods", new CreateFoodRequest("Rice", "Grains", 100, "g", 130, 3, 28, 1, 1, []));
        var response = await client.GetAsync("/meals/ai-recommendations?mealType=Lunch&goal=Maintain");
        Assert.Equal(incomplete ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.GatewayTimeout, response.StatusCode);
        Assert.Contains(incomplete ? "diet.profile_incomplete" : "diet.ai_timeout", await response.Content.ReadAsStringAsync());
    }

    private sealed class MetadataProfile(bool incomplete) : IProfileProvider
    {
        public Task<UserProfileSnapshot> GetAsync(Guid userId, CancellationToken ct) => Task.FromResult(
            new UserProfileSnapshot(DateOnly.FromDateTime(DateTime.UtcNow).AddYears(-30), true, 175m,
                "ModeratelyActive", incomplete ? null : "None", incomplete ? null : []));
    }
    private sealed class SlowRecommendation : IMealRecommendationProvider
    {
        public async Task<MealRecommendationResult> GetAsync(MealRecommendationInput input, DateTimeOffset deadline, CancellationToken ct)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            throw new InvalidOperationException("The caller must cancel the dependency.");
        }
    }

    private sealed class ProfileStub : IProfileProvider
    {
        public Task<UserProfileSnapshot> GetAsync(Guid userId, CancellationToken ct) => Task.FromResult(
            new UserProfileSnapshot(DateOnly.FromDateTime(DateTime.UtcNow).AddYears(-30), true, 175m,
                "ModeratelyActive", "None", ["Peanuts"]));
    }
    private sealed class WeightStub : IWeightProvider
    {
        public static readonly Guid Source = Guid.NewGuid();
        public Task<WeightSnapshot?> GetLatestAsync(Guid userId, DateTimeOffset asOf, CancellationToken ct) =>
            Task.FromResult<WeightSnapshot?>(new(70m, asOf.AddDays(-1), Source, asOf));
    }
    private sealed class MissingWeight : IWeightProvider
    {
        public Task<WeightSnapshot?> GetLatestAsync(Guid userId, DateTimeOffset asOf, CancellationToken ct) => Task.FromResult<WeightSnapshot?>(null);
    }
    [Fact]
    public async Task ManualSave_WithProfileAndWeight_AttachesBmrAndTdee()
    {
        using var host = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.AddScoped<IProfileProvider, ProfileStub>();
            services.AddScoped<IWeightProvider, WeightStub>();
        }));
        using var tokenClient = factory.UserClient(Guid.NewGuid());
        using var client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization = tokenClient.DefaultRequestHeaders.Authorization;

        var response = await client.PutAsJsonAsync("/targets/me", new UpdateTargetRequest(2000, 125, 250, 55));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = (await response.Content.ReadFromJsonAsync<TargetResponse>())!;
        Assert.Equal(1648.75m, result.Bmr);
        Assert.Equal(2555.5625m, result.Tdee);
        Assert.Equal(2000m, result.TargetCalories);
    }

    [Fact]
    public async Task ManualSave_WithoutWeight_StillSavesTargetWithoutSnapshot()
    {
        using var host = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.AddScoped<IProfileProvider, ProfileStub>();
            services.AddScoped<IWeightProvider, MissingWeight>();
        }));
        using var tokenClient = factory.UserClient(Guid.NewGuid());
        using var client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization = tokenClient.DefaultRequestHeaders.Authorization;

        var response = await client.PutAsJsonAsync("/targets/me", new UpdateTargetRequest(2000, 125, 250, 55));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = (await response.Content.ReadFromJsonAsync<TargetResponse>())!;
        Assert.Null(result.Bmr);
        Assert.Null(result.Tdee);
        Assert.Equal(2000m, result.TargetCalories);
    }

    private sealed class RecommendationStub : IMealRecommendationProvider
    {
        public MealRecommendationInput? Input { get; private set; }
        public Task<MealRecommendationResult> GetAsync(MealRecommendationInput input, DateTimeOffset deadline, CancellationToken ct)
        {
            Input = input;
            var food = input.Foods.First();
            return Task.FromResult(new MealRecommendationResult(
                [new(food.Id, food.Name, food.ServingSize, food.ServingUnit, food.Nutrition.Calories,
                    food.Nutrition.ProteinGrams, food.Nutrition.CarbsGrams, food.Nutrition.FatGrams,
                    "Rice", "Reference", [new(4, "Meals", 10, 12, "quote", false, "chapter4.md")])],
                "", [], "General information", false, ""));
        }
    }
}
