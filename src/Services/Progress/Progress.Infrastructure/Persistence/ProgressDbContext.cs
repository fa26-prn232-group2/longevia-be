using Microsoft.EntityFrameworkCore;
using Progress.Application.Abstractions;

namespace Progress.Infrastructure.Persistence;

public sealed class ProgressDbContext(DbContextOptions<ProgressDbContext> options)
    : DbContext(options), IUnitOfWork
{
    public DbSet<Progress.Domain.Entities.FastingSession> FastingSessions => Set<Progress.Domain.Entities.FastingSession>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ProgressDbContext).Assembly);
    }
}
