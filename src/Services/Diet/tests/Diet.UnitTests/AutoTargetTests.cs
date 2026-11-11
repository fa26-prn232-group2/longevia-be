using Diet.Application.Abstractions;
using Diet.Application.Features.Targets;
using Diet.Domain.Entities;
using Longevia.Shared.Auth;

namespace Diet.UnitTests;

public sealed class AutoTargetTests
{
    private static readonly Guid UserId = Guid.NewGuid();

    [Theory]
    [InlineData(null, 2760, 112)]
    [InlineData("LoseWeight", 2350, 144)]
    [InlineData("BuildMuscle", 3030, 144)]
    [InlineData("Longevity", 2760, 112)]
    public async Task Auto_UsesTdeeAndGoal(string? goal, decimal expectedCalories, decimal expectedProtein)
    {
        var useCase = CreateUseCases(weightKg: 80m, goal: "Maintain");

        var result = await useCase.AutoAsync(new AutoTargetRequest(goal), CancellationToken.None);

        Assert.Equal(expectedCalories, result.TargetCalories);
        Assert.Equal(expectedProtein, result.TargetProteinGrams);
        Assert.NotNull(result.Bmr);
        Assert.NotNull(result.Tdee);
        // Macro phải khớp calo trong ±5% để entity chấp nhận.
        var fromMacros = result.TargetProteinGrams * 4 + result.TargetCarbsGrams * 4
            + result.TargetFatGrams * 9;
        Assert.True(Math.Abs(fromMacros - result.TargetCalories) <= result.TargetCalories * 0.05m);
    }

    [Fact]
    public async Task Auto_FallsBackToProfileGoalWhenRequestOmitsIt()
    {
        var useCase = CreateUseCases(weightKg: 80m, goal: "LoseWeight");

        var result = await useCase.AutoAsync(new AutoTargetRequest(), CancellationToken.None);

        Assert.Equal(2350m, result.TargetCalories);
    }

    [Fact]
    public async Task Auto_WithoutWeight_ReturnsUnprocessable()
    {
        var useCase = CreateUseCases(weightKg: null, goal: "Maintain");

        var error = await Assert.ThrowsAsync<Longevia.Shared.Errors.AppException>(() =>
            useCase.AutoAsync(new AutoTargetRequest(), CancellationToken.None));

        Assert.Equal(422, error.Status);
        Assert.Equal("diet.weight_not_found", error.Code);
    }

    private static TargetUseCases CreateUseCases(decimal? weightKg, string goal)
    {
        var profile = new UserProfileSnapshot(
            DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-30)), true, 180m,
            "ModeratelyActive", "None", null, goal);
        var weight = weightKg is null
            ? null
            : new WeightSnapshot(weightKg.Value, DateTimeOffset.UtcNow.AddDays(-1),
                Guid.NewGuid(), DateTimeOffset.UtcNow);
        return new TargetUseCases(new FakeTargets(), new FakeUser(UserId), TimeProvider.System,
            new FakeProfiles(profile), new FakeWeights(weight));
    }

    private sealed class FakeUser(Guid userId) : ICurrentUser
    {
        public Guid? UserId => userId;
        public string? Email => "tester@example.com";
        public bool IsAuthenticated => true;
        public bool InRole(string role) => false;
    }

    private sealed class FakeProfiles(UserProfileSnapshot profile) : IProfileProvider
    {
        public Task<UserProfileSnapshot> GetAsync(Guid userId, CancellationToken ct) =>
            Task.FromResult(profile);
    }

    private sealed class FakeWeights(WeightSnapshot? weight) : IWeightProvider
    {
        public Task<WeightSnapshot?> GetLatestAsync(Guid userId, DateTimeOffset asOf,
            CancellationToken ct) => Task.FromResult(weight);
    }

    private sealed class FakeTargets : ITargetRepository
    {
        private DailyDietTarget? _target;

        public Task<DailyDietTarget?> GetAsync(Guid userId, CancellationToken ct) =>
            Task.FromResult(_target);

        public Task<DailyDietTarget> UpsertAsync(DailyDietTarget target, CancellationToken ct)
        {
            _target = target;
            return Task.FromResult(target);
        }
    }
}
