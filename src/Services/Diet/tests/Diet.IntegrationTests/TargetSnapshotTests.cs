using Diet.Domain;
using Diet.Domain.Entities;
using Diet.Infrastructure.Persistence;
using Diet.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace Diet.IntegrationTests;

public sealed class TargetSnapshotTests(DietApiFactory factory) : IClassFixture<DietApiFactory>
{
    [Theory]
    [InlineData(true, 1648.75, 2555.5625)]
    [InlineData(false, 1482.75, 2298.2625)]
    public async Task PersistsInputsAndCalculationWithoutRequeryingWeight(bool male, decimal bmr, decimal tdee)
    {
        _ = factory.CreateClient();
        var id = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var source = Guid.NewGuid();
        var energy = new EnergyCalculation(70m, now.AddDays(-1), source, now, 175m, 30, male, 1.55m, now);
        var values = new NutritionValues(2000m, 150m, 200m, 66.667m, 0m);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var repository = new TargetRepository(scope.ServiceProvider.GetRequiredService<DietDbContext>());
            await repository.UpsertAsync(DailyDietTarget.CreateCalculated(id, values, energy), default);
        }
        await using var read = factory.Services.CreateAsyncScope();
        var repo = new TargetRepository(read.ServiceProvider.GetRequiredService<DietDbContext>());
        var loaded = (await repo.GetAsync(id, default))!;
        Assert.NotNull(loaded.Calculation);
        Assert.Equal(source, loaded.Calculation.SourceMetricId);
        Assert.Equal(70m, loaded.Calculation.WeightKg);
        Assert.Equal(bmr, loaded.Calculation.Bmr);
        Assert.Equal(tdee, loaded.Calculation.Tdee);
        var originalId = loaded.Id;
        var manual = await repo.UpsertAsync(DailyDietTarget.Create(id, values, now.AddMinutes(1)), default);
        Assert.Equal(originalId, manual.Id);
        Assert.Null(manual.Calculation);
    }
}
