using System.Data;
using System.Security.Cryptography;
using System.Text.Json;
using Diet.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Diet.Infrastructure.Messaging;

public sealed class MealActivitySnapshotExporter(DietDbContext db, TimeProvider time)
{
    public async Task<long> ExportAsync(string destination, CancellationToken ct = default)
    {
        var path = Path.GetFullPath(destination);
        if (File.Exists(path))
        {
            throw new IOException("Snapshot destination already exists.");
        }
        var temporaryPath = path + ".partial-" + Guid.NewGuid().ToString("N");
        try
        {
            var count = await WriteAsync(temporaryPath, ct);
            File.Move(temporaryPath, path);
            return count;
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private async Task<long> WriteAsync(string path, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        await using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await using var writer = new Utf8JsonWriter(stream);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var count = 0L;
        writer.WriteStartObject();
        writer.WriteNumber("schemaVersion", 1);
        writer.WriteString("snapshotId", Guid.NewGuid());
        writer.WriteString("capturedAtUtc", time.GetUtcNow());
        writer.WriteStartArray("entries");
        // shortcut: holds one DB snapshot during export; use a resumable export job for very large histories.
        await foreach (var meal in db.MealLogs.AsNoTracking().OrderBy(x => x.Id).AsAsyncEnumerable().WithCancellation(ct))
        {
            var entry = JsonSerializer.SerializeToUtf8Bytes(new
            {
                mealLogId = meal.Id,
                userId = meal.UserId,
                businessDate = meal.Date,
                createdAt = meal.CreatedAt,
            });
            writer.WriteRawValue(entry);
            hash.AppendData(entry);
            hash.AppendData("\n"u8);
            count++;
            await writer.FlushAsync(ct);
        }
        writer.WriteEndArray();
        writer.WriteNumber("recordCount", count);
        writer.WriteString("entriesSha256", Convert.ToHexString(hash.GetHashAndReset()));
        writer.WriteEndObject();
        await writer.FlushAsync(ct);
        await transaction.CommitAsync(ct);
        return count;
    }
}
