using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Progress.Infrastructure.Persistence;

public sealed class ProgressDbContextFactory : IDesignTimeDbContextFactory<ProgressDbContext>
{
    public ProgressDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__Progress")
            ?? "Host=localhost;Database=progress_db;Username=progress_user;Password=change-me";
        return new(new DbContextOptionsBuilder<ProgressDbContext>().UseNpgsql(connection).Options);
    }
}
