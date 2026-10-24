using Microsoft.EntityFrameworkCore;
using Progress.Application.Abstractions;

namespace Progress.Infrastructure.Persistence;

public sealed class ProgressDbContext(DbContextOptions<ProgressDbContext> options)
    : DbContext(options), IUnitOfWork
{
    public DbSet<Progress.Domain.Entities.FastingSession> FastingSessions => Set<Progress.Domain.Entities.FastingSession>();
    public DbSet<Progress.Domain.Entities.WaterLog> WaterLogs => Set<Progress.Domain.Entities.WaterLog>();
    public DbSet<Progress.Domain.Entities.SleepLog> SleepLogs => Set<Progress.Domain.Entities.SleepLog>();
    public DbSet<Progress.Domain.Entities.BodyMetricLog> BodyMetricLogs => Set<Progress.Domain.Entities.BodyMetricLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ProgressDbContext).Assembly);
    }
}
