using Longevia.Shared.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Progress.Application.Abstractions;
using Progress.Domain.Entities;
using Progress.Infrastructure.Persistence;
using Progress.Infrastructure.Services;

namespace Progress.IntegrationTests.Messaging;

[Collection("Progress foundation")]
public sealed class MealIntegrityTests(ProgressFixture fixture)
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task PartialPair_ValidReplay_RepairsMissingHalfAtomically(bool inboxOnly)
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ProgressDbContext>();
        var id = Guid.NewGuid();
        var user = Guid.NewGuid();
        var instant = DateTimeOffset.Parse("2026-10-25T00:00:00Z");
        var day = new DateOnly(2026, 1, 15);
        if (inboxOnly) db.ProcessedMealEvents.Add(ProcessedMealEvent.Create(id, id, user, day, instant));
        else db.ActivityContributions.Add(ActivityContribution.Create(user, "Diet.Meal", id.ToString(), day, instant));
        await db.SaveChangesAsync();
        var message = new MealLoggedEvent(id, user, id, day, instant, instant);
        var service = scope.ServiceProvider.GetRequiredService<IMealActivityConsumerService>();
        await service.ProcessMealLoggedAsync(message, default);
        Assert.Equal(1, await db.ProcessedMealEvents.CountAsync(x => x.EventId == id && x.MealLogId == id));
        Assert.Equal(1, await db.ActivityContributions.CountAsync(x => x.SourceLogId == id.ToString() && x.UserId == user && x.Date == day));
        Assert.Equal(MealProcessingResult.AlreadyProcessed, await service.ProcessMealLoggedAsync(message, default));
    }

    [Fact]
    public async Task CanonicalIdentity_MismatchedEventId_RejectsWithoutPersistence()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IMealActivityConsumerService>();
        var message = new MealLoggedEvent(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2026, 1, 15),
            DateTimeOffset.Parse("2026-10-25T00:00:00Z"), DateTimeOffset.Parse("2026-10-25T00:00:00Z"));
        await Assert.ThrowsAsync<ArgumentException>(() => service.ProcessMealLoggedAsync(message, default));
        var db = scope.ServiceProvider.GetRequiredService<ProgressDbContext>();
        Assert.False(await db.ProcessedMealEvents.AnyAsync(x => x.EventId == message.EventId));
    }

    [Fact]
    public async Task ExistingInbox_WrongMealAssociation_RejectsWithoutRepair()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ProgressDbContext>();
        var id = Guid.NewGuid();
        var user = Guid.NewGuid();
        var instant = DateTimeOffset.Parse("2026-10-25T00:00:00Z");
        var day = new DateOnly(2026, 1, 15);
        db.ProcessedMealEvents.Add(ProcessedMealEvent.Create(id, Guid.NewGuid(), user, day, instant));
        await db.SaveChangesAsync();
        var service = scope.ServiceProvider.GetRequiredService<IMealActivityConsumerService>();
        Assert.Equal(MealProcessingResult.ConflictingDuplicate,
            await service.ProcessMealLoggedAsync(new(id, user, id, day, instant, instant), default));
        Assert.False(await db.ActivityContributions.AnyAsync(x => x.SourceLogId == id.ToString()));
    }

    [Fact]
    public async Task UnrelatedUniqueViolation_ThrowsAndRollsBackInbox()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var original = scope.ServiceProvider.GetRequiredService<ProgressDbContext>();
        var occupied = ActivityContribution.Create(Guid.NewGuid(), "Test", Guid.NewGuid().ToString(), new DateOnly(2026, 1, 15), DateTimeOffset.UtcNow);
        original.ActivityContributions.Add(occupied);
        await original.SaveChangesAsync();
        await using var db = new ProgressDbContext(new DbContextOptionsBuilder<ProgressDbContext>()
            .UseNpgsql(fixture.ConnectionString).AddInterceptors(new CollisionInterceptor(occupied.Id)).Options);
        var id = Guid.NewGuid();
        var service = new MealActivityConsumerService(db, NullLogger<MealActivityConsumerService>.Instance, new FoundationTimeProvider());
        var message = new MealLoggedEvent(id, Guid.NewGuid(), id, new DateOnly(2026, 1, 15), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => service.ProcessMealLoggedAsync(message, default));
        Assert.Equal(Npgsql.PostgresErrorCodes.UniqueViolation, Assert.IsType<Npgsql.PostgresException>(exception.InnerException).SqlState);
        Assert.False(await original.ProcessedMealEvents.AnyAsync(x => x.EventId == id));
    }

    private sealed class CollisionInterceptor(Guid id) : Microsoft.EntityFrameworkCore.Diagnostics.SaveChangesInterceptor
    {
        public override ValueTask<Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<int>> SavingChangesAsync(
            Microsoft.EntityFrameworkCore.Diagnostics.DbContextEventData eventData,
            Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            foreach (var entry in eventData.Context!.ChangeTracker.Entries<ActivityContribution>().Where(x => x.State == EntityState.Added))
                entry.Property(x => x.Id).CurrentValue = id;
            return ValueTask.FromResult(result);
        }
    }
}
