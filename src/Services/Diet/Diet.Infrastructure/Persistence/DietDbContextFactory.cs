using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Diet.Infrastructure.Persistence;

public sealed class DietDbContextFactory : IDesignTimeDbContextFactory<DietDbContext>
{
    public DietDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__Diet")
            ?? "Host=localhost;Database=diet_db;Username=diet_user;Password=change-me";
        return new(new DbContextOptionsBuilder<DietDbContext>().UseNpgsql(connection).Options);
    }
}
