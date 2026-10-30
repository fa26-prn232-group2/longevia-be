using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Progress.Domain.Entities;
using Progress.Infrastructure.Persistence;

namespace Progress.IntegrationTests.Messaging;

[Collection("Progress foundation")]
public sealed class SnapshotReconciliationTests(ProgressFixture fixture)
{
    [Theory]
    [InlineData("empty", 0)]
    [InlineData("match", 0)]
    [InlineData("missing-contribution", 2)]
    [InlineData("missing-inbox", 2)]
    [InlineData("user", 2)]
    [InlineData("date", 2)]
    [InlineData("association", 2)]
    [InlineData("duplicate", 3)]
    [InlineData("checksum", 3)]
    [InlineData("count", 3)]
    [InlineData("truncated", 3)]
    [InlineData("version", 3)]
    [InlineData("unavailable", 4)]
    [InlineData("extra-live", 0)]
    [InlineData("multi-batch", 0)]
    [InlineData("backdated", 0)]
    [InlineData("empty-id", 3)]
    [InlineData("non-utc", 3)]
    [InlineData("invalid-date", 3)]
    [InlineData("wrong-db", 4)]
    public async Task Reconcile_ActualProcess_ReportsVerifiedDatabaseResult(string scenario, int expectedExit)
    {
        await using var emptyDatabase = scenario == "empty" ? new Testcontainers.PostgreSql.PostgreSqlBuilder("postgres:17-alpine")
            .WithDatabase("progress_db").WithUsername("progress_user").WithPassword(Guid.NewGuid().ToString("N")).Build() : null;
        if (emptyDatabase is not null)
        {
            await emptyDatabase.StartAsync();
            await using var emptyContext = new ProgressDbContext(new DbContextOptionsBuilder<ProgressDbContext>()
                .UseNpgsql(emptyDatabase.GetConnectionString()).Options);
            await emptyContext.Database.MigrateAsync();
        }
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ProgressDbContext>();
        await db.Database.MigrateAsync();
        var entries = new List<object>();
        var count = scenario == "empty" ? 0 : scenario == "multi-batch" ? 1003 : 1;
        for (var index = 0; index < count; index++)
        {
            var id = Guid.NewGuid();
            var user = Guid.NewGuid();
            var day = new DateOnly(2026, 1, 15);
            var created = DateTimeOffset.Parse("2026-10-25T00:00:00Z");
            entries.Add(new { mealLogId = id, userId = user, businessDate = day, createdAt = created });
            if (scenario != "missing-contribution")
                db.ActivityContributions.Add(ActivityContribution.Create(scenario == "user" ? Guid.NewGuid() : user,
                    "Diet.Meal", id.ToString(), scenario == "date" ? day.AddDays(1) : day, created));
            if (scenario != "missing-inbox")
                db.ProcessedMealEvents.Add(ProcessedMealEvent.Create(id,
                    scenario == "association" ? Guid.NewGuid() : id, user, day, created));
        }
        if (scenario == "extra-live")
        {
            var id = Guid.NewGuid();
            var user = Guid.NewGuid();
            var day = new DateOnly(2026, 10, 26);
            var instant = DateTimeOffset.Parse("2026-10-26T00:00:00Z");
            db.ActivityContributions.Add(ActivityContribution.Create(user, "Diet.Meal", id.ToString(), day, instant));
            db.ProcessedMealEvents.Add(ProcessedMealEvent.Create(id, id, user, day, instant));
        }
        await db.SaveChangesAsync();
        if (scenario == "duplicate") entries.Add(entries[0]);
        var rawEntries = entries.Select(x => JsonSerializer.Serialize(x)).ToArray();
        var checksum = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Concat(rawEntries.Select(x => x + "\n")))));
        var root = JsonNode.Parse(JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            snapshotId = Guid.NewGuid(),
            capturedAtUtc = DateTimeOffset.Parse("2026-10-25T00:00:00Z"),
            entries,
            recordCount = entries.Count,
            entriesSha256 = checksum
        }))!;
        // Hash the actual bytes emitted by JsonNode, which may escape '+' differently.
        using (var serialized = JsonDocument.Parse(root.ToJsonString()))
        {
            root["entriesSha256"] = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Concat(
                serialized.RootElement.GetProperty("entries").EnumerateArray().Select(x => x.GetRawText() + "\n")))));
        }
        if (scenario == "checksum") root["entriesSha256"] = new string('0', 64);
        if (scenario == "count") root["recordCount"] = 999;
        if (scenario == "version") root["schemaVersion"] = 2;
        if (scenario == "empty-id") root["snapshotId"] = Guid.Empty.ToString();
        if (scenario == "non-utc") root["capturedAtUtc"] = "2026-10-25T07:00:00+07:00";
        if (scenario == "invalid-date") root["entries"]![0]!["businessDate"] = "2026-02-30";
        var path = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(path, scenario == "truncated" ? "{\"entries\":[" : root.ToJsonString());
            var start = new ProcessStartInfo("dotnet") { RedirectStandardOutput = true, RedirectStandardError = true };
            start.ArgumentList.Add(typeof(Program).Assembly.Location);
            start.ArgumentList.Add("--reconcile-meal-snapshot");
            start.ArgumentList.Add(path);
            start.Environment["ConnectionStrings__Progress"] = scenario switch
            {
                "unavailable" => "Host=127.0.0.1;Port=1;Database=progress_db;Username=x;Password=x;Timeout=1",
                "wrong-db" => fixture.ConnectionString.Replace("progress_db", "diet_db"),
                _ => emptyDatabase?.GetConnectionString() ?? fixture.ConnectionString
            };
            using var process = Process.Start(start)!;
            var output = process.StandardOutput.ReadToEndAsync();
            var error = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(45));
            var result = await output;
            Assert.True(process.ExitCode == expectedExit, $"exit={process.ExitCode}; {result}; {await error}");
            Assert.Contains(expectedExit == 0 ? "PASS:" : "FAIL:", result);
            if (expectedExit != 0) Assert.DoesNotContain("PASS:", result);
            if (expectedExit == 0) Assert.Contains($"Matched: {count};", result);
        }
        finally { File.Delete(path); }
    }
}
