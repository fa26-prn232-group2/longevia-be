using System.Data;
using Diet.Application.Abstractions;
using Diet.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Diet.Infrastructure.Messaging;

public sealed class MealActivityReplay(DietDbContext db, IMealActivityPublisher publisher)
{
    public async Task<int> EnqueueAsync(CancellationToken ct = default)
    {
        // One snapshot includes old logs while concurrent creates publish through their own outbox.
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        var count = 0;
        while (true)
        {
            var batch = await db.MealLogs.AsNoTracking().OrderBy(x => x.Id)
                .Skip(count).Take(100).ToListAsync(ct);
            if (batch.Count == 0)
            {
                break;
            }
            foreach (var meal in batch)
            {
                await publisher.PublishAsync(meal, ct);
            }
            await db.SaveChangesAsync(ct);
            count += batch.Count;
        }
        await transaction.CommitAsync(ct);
        return count;
    }
}
