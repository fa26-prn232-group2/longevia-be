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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DietDbContext).Assembly);
        FoodSeed.Configure(modelBuilder);
        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();
    }
}
