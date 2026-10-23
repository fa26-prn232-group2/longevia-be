using Identity.Application.Abstractions;

namespace Identity.Infrastructure.Persistence;

public sealed class EfUnitOfWork(IdentityDbContext dbContext) : IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken ct = default) =>
        dbContext.SaveChangesAsync(ct);
}
