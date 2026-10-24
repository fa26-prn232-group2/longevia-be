using Identity.Application.Abstractions;
using Microsoft.EntityFrameworkCore.Storage;

namespace Identity.Infrastructure.Persistence;

public sealed class EfUnitOfWork(IdentityDbContext dbContext) : IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken ct = default) =>
        dbContext.SaveChangesAsync(ct);

    public async Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken ct = default) =>
        new EfUnitOfWorkTransaction(await dbContext.Database.BeginTransactionAsync(ct));

    private sealed class EfUnitOfWorkTransaction(IDbContextTransaction transaction) : IUnitOfWorkTransaction
    {
        public Task CommitAsync(CancellationToken ct = default) => transaction.CommitAsync(ct);

        public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }
}
