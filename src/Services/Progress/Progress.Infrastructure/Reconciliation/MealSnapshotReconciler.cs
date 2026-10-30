using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Npgsql;

namespace Progress.Infrastructure.Reconciliation;

public static class MealSnapshotReconciler
{
    public static async Task<int> RunAsync(string path, string? connectionString, TextWriter output,
        CancellationToken ct = default)
    {
        JsonDocument document;
        try
        {
            await using var file = File.OpenRead(path);
            document = await JsonDocument.ParseAsync(file, cancellationToken: ct);
            try { Validate(document.RootElement); }
            catch { document.Dispose(); throw; }
        }
        catch (Exception ex) when (ex is JsonException or IOException or ArgumentException
            or InvalidOperationException or FormatException or KeyNotFoundException or OverflowException)
        {
            await output.WriteLineAsync("FAIL: invalid snapshot; checksum NOT VERIFIED (exit 3).");
            return 3;
        }

        using (document)
        {
            var root = document.RootElement;
            await output.WriteLineAsync($"Snapshot ID: {root.GetProperty("snapshotId").GetGuid()}; entries: {root.GetProperty("recordCount").GetInt64()}; checksum: VERIFIED");
            try
            {
                if (string.IsNullOrWhiteSpace(connectionString)
                    || new NpgsqlConnectionStringBuilder(connectionString).Database != "progress_db")
                    throw new ArgumentException("Explicit progress_db connection required.");
                await using var connection = new NpgsqlConnection(connectionString);
                await connection.OpenAsync(ct);
                await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
                await using (var readOnly = new NpgsqlCommand("SET TRANSACTION READ ONLY", connection, transaction))
                    await readOnly.ExecuteNonQueryAsync(ct);
                // Empty snapshots still require an actual, migrated projection database.
                await using (var schema = new NpgsqlCommand("""
                    SELECT "SourceLogId" FROM activity_contributions LIMIT 0;
                    SELECT "EventId" FROM processed_meal_events LIMIT 0;
                    """, connection, transaction))
                    await schema.ExecuteNonQueryAsync(ct);
                long matched = 0, missingContribution = 0, missingInbox = 0, userDateMismatch = 0, otherMismatch = 0;
                foreach (var batch in root.GetProperty("entries").EnumerateArray().Chunk(500))
                {
                    var ids = batch.Select(x => x.GetProperty("mealLogId").GetGuid()).ToArray();
                    await using var command = new NpgsqlCommand("""
                        SELECT 'c', "SourceLogId", "UserId", "Date", NULL::uuid, NULL::uuid
                        FROM activity_contributions WHERE "Source" = 'Diet.Meal' AND "SourceLogId" = ANY(@texts)
                        UNION ALL
                        SELECT 'i', "EventId"::text, "UserId", "BusinessDate", "MealLogId", "EventId"
                        FROM processed_meal_events WHERE "EventId" = ANY(@ids) OR "MealLogId" = ANY(@ids)
                        """, connection, transaction);
                    command.Parameters.AddWithValue("ids", ids);
                    command.Parameters.AddWithValue("texts", ids.Select(x => x.ToString()).ToArray());
                    var contributions = new Dictionary<string, List<(Guid User, DateOnly Date)>>();
                    var inbox = new List<(Guid Event, Guid Meal, Guid User, DateOnly Date)>();
                    await using (var reader = await command.ExecuteReaderAsync(ct))
                    {
                        while (await reader.ReadAsync(ct))
                        {
                            if (reader.GetString(0) == "c")
                            {
                                var id = reader.GetString(1);
                                if (!contributions.TryGetValue(id, out var rows)) contributions[id] = rows = [];
                                rows.Add((reader.GetGuid(2), reader.GetFieldValue<DateOnly>(3)));
                            }
                            else inbox.Add((reader.GetGuid(5), reader.GetGuid(4), reader.GetGuid(2), reader.GetFieldValue<DateOnly>(3)));
                        }
                    }
                    foreach (var entry in batch)
                    {
                        var id = entry.GetProperty("mealLogId").GetGuid();
                        var user = entry.GetProperty("userId").GetGuid();
                        var date = DateOnly.ParseExact(entry.GetProperty("businessDate").GetString()!, "yyyy-MM-dd", CultureInfo.InvariantCulture);
                        contributions.TryGetValue(id.ToString(), out var rows);
                        var events = inbox.Where(x => x.Event == id || x.Meal == id).ToArray();
                        var missingC = rows is null || rows.Count == 0;
                        var missingI = !events.Any(x => x.Event == id);
                        var mismatch = (rows?.Any(x => x.User != user || x.Date != date) ?? false)
                            || events.Any(x => x.User != user || x.Date != date);
                        var integrity = (rows is not null && rows.Count != 1)
                            || (events.Length > 0 && (events.Length != 1 || events.Any(x => x.Meal != id || x.Event != id)));
                        if (missingC) missingContribution++;
                        if (missingI) missingInbox++;
                        if (mismatch) userDateMismatch++;
                        if (integrity) otherMismatch++;
                        if (!missingC && !missingI && !mismatch && !integrity) matched++;
                    }
                }
                await transaction.CommitAsync(ct);
                var pass = matched == root.GetProperty("recordCount").GetInt64();
                await output.WriteLineAsync($"Matched: {matched}; missing contributions: {missingContribution}; missing inbox: {missingInbox}; user/date mismatches: {userDateMismatch}; other integrity mismatches: {otherMismatch}; additional live records: NOT EVALUATED (outside snapshot scope)");
                await output.WriteLineAsync(pass ? "PASS: historical snapshot entries reconciled against progress_db." : "FAIL: persisted projection mismatch (exit 2).");
                return pass ? 0 : 2;
            }
            catch (Exception ex) when (ex is NpgsqlException or ArgumentException or InvalidOperationException or TimeoutException or OperationCanceledException)
            {
                await output.WriteLineAsync("FAIL: database comparison unavailable or incomplete (exit 4).");
                return 4;
            }
        }
    }

    private static void Validate(JsonElement root)
    {
        RequireUniqueProperties(root);
        if (root.GetProperty("schemaVersion").GetInt32() != 1
            || root.GetProperty("snapshotId").GetGuid() == Guid.Empty
            || !IsUtc(root.GetProperty("capturedAtUtc")))
            throw new FormatException();
        var ids = new HashSet<Guid>();
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        long count = 0;
        foreach (var entry in root.GetProperty("entries").EnumerateArray())
        {
            RequireUniqueProperties(entry);
            var id = entry.GetProperty("mealLogId").GetGuid();
            if (id == Guid.Empty || !ids.Add(id) || entry.GetProperty("userId").GetGuid() == Guid.Empty
                || !DateOnly.TryParseExact(entry.GetProperty("businessDate").GetString(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _)
                || !IsUtc(entry.GetProperty("createdAt")))
                throw new FormatException();
            hash.AppendData(Encoding.UTF8.GetBytes(entry.GetRawText()));
            hash.AppendData("\n"u8);
            count++;
        }
        if (root.GetProperty("recordCount").GetInt64() != count
            || root.GetProperty("entriesSha256").GetString() != Convert.ToHexString(hash.GetHashAndReset()))
            throw new FormatException();
    }

    private static bool IsUtc(JsonElement value)
    {
        var text = value.GetString();
        return text is not null && (text.EndsWith('Z') || text.EndsWith("+00:00", StringComparison.Ordinal))
            && value.GetDateTimeOffset().Offset == TimeSpan.Zero;
    }

    private static void RequireUniqueProperties(JsonElement value)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in value.EnumerateObject())
            if (!names.Add(property.Name)) throw new FormatException();
    }
}
