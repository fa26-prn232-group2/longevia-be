using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Progress.Domain;
using Progress.Domain.Entities;
using Progress.Infrastructure.Persistence;

if (args.Length < 2 || args[0] is not ("init" or "seed" or "verify") || (args[0] == "init" && args.Length != 6))
    throw new ArgumentException("Expected init <output> <progress-port> <identity-port> <pg-port> <sha>, seed <output>, or verify <output>.");
var output = Path.GetFullPath(args[1]);
Directory.CreateDirectory(output);
var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
var envPath = Path.Combine(output, ".env");
if (args[0] == "init")
{
    using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    var secret = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
    var publicKey = Convert.ToBase64String(Encoding.UTF8.GetBytes(key.ExportSubjectPublicKeyInfoPem()));
    var privateKey = Convert.ToBase64String(Encoding.UTF8.GetBytes(key.ExportPkcs8PrivateKeyPem()));
    File.WriteAllText(envPath, $"BENCH_PASSWORD={secret}\nJWT_PUBLIC={publicKey}\nJWT_PRIVATE={privateKey}\nPROGRESS_PORT={args[2]}\nIDENTITY_PORT={args[3]}\nPG_PORT={args[4]}\nGIT_SHA={args[5]}\n");
    return;
}

var env = File.ReadAllLines(envPath).Select(line => line.Split('=', 2)).ToDictionary(x => x[0], x => x[1]);
if (args[0] == "verify")
{
    var ownConnection = $"Host=127.0.0.1;Port={env["PG_PORT"]};Database=progress_db;Username=progress_user;Password={env["BENCH_PASSWORD"]};Maximum Pool Size=20";
    await using var ownDb = new ProgressDbContext(new DbContextOptionsBuilder<ProgressDbContext>().UseNpgsql(ownConnection).Options);
    var cutoff = new DateTimeOffset(2026, 10, 25, 0, 0, 0, TimeSpan.Zero);
    var logged = ownDb.BodyMetricLogs.AsNoTracking().Where(metric => metric.LoggedAt > cutoff);
    var expectedBmi = BodyMetricLog.Create(Guid.NewGuid(), cutoff, 70m, 175.25m).Bmi;
    var count = await logged.CountAsync();
    var invalid = await logged.CountAsync(metric => metric.HeightUsedCm != 175.25m || metric.WeightKg != 70m || metric.Bmi != expectedBmi || metric.BmiCategory != "NormalWeight");
    if (count == 0 || invalid != 0) throw new InvalidOperationException("Measured metric snapshot verification failed.");
    var verification = new { measuredMetrics = count, invalidSnapshots = invalid, heightUsedCm = 175.25m, bmi = expectedBmi };
    File.WriteAllText(Path.Combine(output, "persistence.json"), JsonSerializer.Serialize(verification, json));
    Console.WriteLine(JsonSerializer.Serialize(verification, json));
    return;
}
using var signingKey = ECDsa.Create();
signingKey.ImportFromPem(Encoding.UTF8.GetString(Convert.FromBase64String(env["JWT_PRIVATE"])));
string Token(Guid user)
{
    static string B64(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    var header = B64(Encoding.UTF8.GetBytes("{\"alg\":\"ES256\",\"typ\":\"JWT\",\"kid\":\"benchmark\"}"));
    var payload = B64(JsonSerializer.SerializeToUtf8Bytes(new
    {
        sub = user.ToString(),
        iss = "progress-benchmark",
        aud = "benchmark-clients",
        exp = DateTimeOffset.UtcNow.AddHours(4).ToUnixTimeSeconds()
    }));
    var content = $"{header}.{payload}";
    return $"{content}.{B64(signingKey.SignData(Encoding.UTF8.GetBytes(content), HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation))}";
}

var usersPath = Path.Combine(output, "users.json");
List<Guid> users;
using var identity = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{env["IDENTITY_PORT"]}") };
if (File.Exists(usersPath)) users = JsonSerializer.Deserialize<List<Guid>>(File.ReadAllText(usersPath))!;
else
{
    users = [];
    for (var i = 0; i < 33; i++)
    {
        using var response = await identity.PostAsJsonAsync("/auth/register", new
        { email = $"bench-{i}@example.test", password = env["BENCH_PASSWORD"] + "aA1!", fullName = "Synthetic Benchmark" });
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException($"Identity seed registration failed: {(int)response.StatusCode}");
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        users.Add(body.RootElement.GetProperty("id").GetGuid());
    }
    File.WriteAllText(usersPath, JsonSerializer.Serialize(users, json));
}
foreach (var user in users)
{
    using var request = new HttpRequestMessage(HttpMethod.Put, "/users/me/profile");
    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Token(user));
    request.Content = JsonContent.Create(new
    {
        birthDate = "1990-01-01",
        biologicalSex = "Male",
        heightCm = 175.25m,
        activityLevel = "Sedentary",
        dietaryPreference = "None",
        allergies = Array.Empty<string>()
    });
    using var response = await identity.SendAsync(request);
    if (!response.IsSuccessStatusCode) throw new InvalidOperationException($"Identity profile seed failed: {(int)response.StatusCode}");
}

// This setup-only connection is constructed for the isolated Compose PostgreSQL, never operator input.
var connection = $"Host=127.0.0.1;Port={env["PG_PORT"]};Database=progress_db;Username=progress_user;Password={env["BENCH_PASSWORD"]};Maximum Pool Size=20";
await using var db = new ProgressDbContext(new DbContextOptionsBuilder<ProgressDbContext>().UseNpgsql(connection).Options);
if (db.Database.HasPendingModelChanges()) throw new InvalidOperationException("Pending Progress model changes.");
await using var transaction = await db.Database.BeginTransactionAsync();
await db.ActivityContributions.ExecuteDeleteAsync();
await db.ProcessedMealEvents.ExecuteDeleteAsync();
await db.BodyMetricLogs.ExecuteDeleteAsync();
await db.WaterLogs.ExecuteDeleteAsync();
await db.SleepLogs.ExecuteDeleteAsync();
await db.FastingSessions.ExecuteDeleteAsync();
var anchor = new DateTimeOffset(2026, 10, 25, 0, 0, 0, TimeSpan.Zero);
for (var i = 0; i < users.Count; i++)
{
    var user = users[i];
    var count = i == 0 ? 10000 : 100;
    for (var j = 0; j < count; j++)
        db.BodyMetricLogs.Add(BodyMetricLog.Create(user, anchor.AddHours(-j - 1), 70m + j % 5, 175.25m));
    for (var day = 0; day < 60; day++)
    {
        var instant = anchor.AddDays(-day - 1);
        var date = UtcCalendar.Day(instant);
        db.WaterLogs.Add(WaterLog.Create(user, 350, date, instant));
        db.SleepLogs.Add(SleepLog.Create(user, instant.AddHours(-8), instant, "Good"));
        var session = FastingSession.Start(user, "16:8", 16, instant.AddHours(-16));
        session.End(instant);
        db.FastingSessions.Add(session);
        var meal = Guid.NewGuid();
        db.ActivityContributions.Add(ActivityContribution.Create(user, "Diet.Meal", meal.ToString(), date, instant.AddDays(1)));
        db.ProcessedMealEvents.Add(ProcessedMealEvent.Create(meal, meal, user, date, instant.AddDays(20)));
    }
    db.FastingSessions.Add(FastingSession.Start(user, "16:8", 16, anchor));
}
await db.SaveChangesAsync();
await transaction.CommitAsync();
var dataset = new
{
    users = users.Count,
    heavyUserMetrics = 10000,
    metrics = await db.BodyMetricLogs.CountAsync(),
    water = await db.WaterLogs.CountAsync(),
    sleep = await db.SleepLogs.CountAsync(),
    fasting = await db.FastingSessions.CountAsync(),
    meals = await db.ActivityContributions.CountAsync(),
    inbox = await db.ProcessedMealEvents.CountAsync(),
    migrations = (await db.Database.GetAppliedMigrationsAsync()).Count(),
    anchorUtc = anchor,
    identity = "actual provider; profiles via Identity HTTP API; no direct Identity DB access"
};
File.WriteAllText(Path.Combine(output, "dataset.json"), JsonSerializer.Serialize(dataset, json));
File.WriteAllText(Path.Combine(output, "clients.json"), JsonSerializer.Serialize(users.Select(user => new { token = Token(user) }), json));
Console.WriteLine(JsonSerializer.Serialize(dataset, json));
