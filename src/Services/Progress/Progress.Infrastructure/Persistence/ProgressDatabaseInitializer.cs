using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Progress.Infrastructure.Persistence;

public static class ProgressDatabaseInitializer
{
    public static async Task InitializeAsync(ProgressDbContext db, CancellationToken ct)
    {
        // Provisioning belongs to deployment; do not create a missing database as a side effect.
        await OpenConnectionAsync(db, ct);
        try
        {
            if (db.Database.GetMigrations().Any())
            {
                await db.Database.MigrateAsync(ct);
            }
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }

    private static async Task OpenConnectionAsync(ProgressDbContext db, CancellationToken ct)
    {
        // The provisioner's temporary socket-only server can pass pg_isready before TCP opens.
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                await db.Database.OpenConnectionAsync(ct);
                return;
            }
            catch (Exception exception) when (attempt < 4 && IsTransient(exception))
            {
                await Task.Delay(TimeSpan.FromSeconds(1), ct);
            }
        }
    }

    private static bool IsTransient(Exception exception) => exception is NpgsqlException { IsTransient: true }
        || exception is InvalidOperationException { InnerException: NpgsqlException { IsTransient: true } };
}
