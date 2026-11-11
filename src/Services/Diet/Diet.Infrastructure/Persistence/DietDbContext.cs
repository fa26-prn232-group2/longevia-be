using Diet.Application.Abstractions;
using Diet.Domain.Entities;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace Diet.Infrastructure.Persistence;

public sealed class DietDbContext(DbContextOptions<DietDbContext> options) : DbContext(options), IUnitOfWork
{
    public DbSet<FoodItem> FoodItems => Set<FoodItem>();
    public DbSet<DailyDietTarget> DailyDietTargets => Set<DailyDietTarget>();
    public DbSet<MealLog> MealLogs => Set<MealLog>();
    public DbSet<MealLogItem> MealLogItems => Set<MealLogItem>();
    public DbSet<MealPlan> MealPlans => Set<MealPlan>();
    public DbSet<KnowledgeConversation> KnowledgeConversations => Set<KnowledgeConversation>();
    public DbSet<KnowledgeMessage> KnowledgeMessages => Set<KnowledgeMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DietDbContext).Assembly);
        FoodSeed.Configure(modelBuilder);
        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();
    }

    public async Task ExecuteInTransactionAsync(string lockKey,
        Func<CancellationToken, Task> action, CancellationToken ct)
    {
        var strategy = Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await Database.BeginTransactionAsync(
                System.Data.IsolationLevel.ReadCommitted, ct);
            await Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(hashtext({0}))",
                [lockKey], ct);
            await action(ct);
            await tx.CommitAsync(ct);
        });
    }
}
