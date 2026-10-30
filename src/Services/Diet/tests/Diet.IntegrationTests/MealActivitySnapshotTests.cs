using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Diet.Domain.Entities;
using Diet.Infrastructure.Messaging;
using Diet.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Diet.IntegrationTests;

public sealed class MealActivitySnapshotTests(DietApiFactory factory) : IClassFixture<DietApiFactory>
{
    [Fact]
    public async Task Export_EmptyAnd101LegacyMeals_ProducesVerifiableManifest_WithoutOverwriting()
    {
        using var client = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var exporter = scope.ServiceProvider.GetRequiredService<MealActivitySnapshotExporter>();
        var db = scope.ServiceProvider.GetRequiredService<DietDbContext>();
        var directory = Path.Combine(Path.GetTempPath(), "diet-snapshot-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var emptyPath = Path.Combine(directory, "empty.json");
            Assert.Equal(0, await exporter.ExportAsync(emptyPath));
            using var empty = JsonDocument.Parse(await File.ReadAllTextAsync(emptyPath));
            Assert.Equal(0, empty.RootElement.GetProperty("recordCount").GetInt64());
            Assert.Equal(Convert.ToHexString(SHA256.HashData(Array.Empty<byte>())),
                empty.RootElement.GetProperty("entriesSha256").GetString());

            var users = new[] { Guid.NewGuid(), Guid.NewGuid() };
            var meals = Enumerable.Range(0, 101).Select(i => MealLog.Create(users[i % 2],
                new DateOnly(2026, 9, 1).AddDays(i % 7), "Lunch", DateTimeOffset.UtcNow)).ToList();
            db.MealLogs.AddRange(meals);
            await db.SaveChangesAsync();
            var path = Path.Combine(directory, "snapshot.json");
            Assert.Equal(101, await exporter.ExportAsync(path));
            var bytes = await File.ReadAllBytesAsync(path);
            using var snapshot = JsonDocument.Parse(bytes);
            var root = snapshot.RootElement;
            Assert.Equal(1, root.GetProperty("schemaVersion").GetInt32());
            Assert.True(Guid.TryParse(root.GetProperty("snapshotId").GetString(), out _));
            var entries = root.GetProperty("entries").EnumerateArray().ToArray();
            Assert.Equal(101, root.GetProperty("recordCount").GetInt64());
            Assert.Equal(101, entries.Length);
            Assert.Equal(meals.Select(x => x.Id).Order(), entries.Select(x => x.GetProperty("mealLogId").GetGuid()).Order());
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            foreach (var entry in entries)
            {
                var original = meals.Single(x => x.Id == entry.GetProperty("mealLogId").GetGuid());
                Assert.Equal(original.UserId, entry.GetProperty("userId").GetGuid());
                Assert.Equal(original.Date.ToString("yyyy-MM-dd"), entry.GetProperty("businessDate").GetString());
                Assert.Equal(original.CreatedAt, entry.GetProperty("createdAt").GetDateTimeOffset());
                hash.AppendData(Encoding.UTF8.GetBytes(entry.GetRawText()));
                hash.AppendData("\n"u8);
            }
            Assert.Equal(Convert.ToHexString(hash.GetHashAndReset()), root.GetProperty("entriesSha256").GetString());
            await Assert.ThrowsAsync<IOException>(() => exporter.ExportAsync(path));
            Assert.Equal(bytes, await File.ReadAllBytesAsync(path));
            Assert.Empty(Directory.GetFiles(directory, "*.partial-*"));
        }
        finally
        {
            foreach (var file in Directory.GetFiles(directory))
            {
                File.Delete(file);
            }
            Directory.Delete(directory);
        }
    }
}
