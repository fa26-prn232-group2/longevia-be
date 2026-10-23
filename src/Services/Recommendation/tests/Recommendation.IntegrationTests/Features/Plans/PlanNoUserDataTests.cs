using System.Collections.Concurrent;
using Longevia.Recommendation.Contracts.V1;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using Recommendation.IntegrationTests.Features.Meals;
using Recommendation.IntegrationTests.Features.Shared;
using CandidateFood = Longevia.Recommendation.Contracts.V1.CandidateFood;
using UserProfile = Longevia.Recommendation.Contracts.V1.UserProfile;

namespace Recommendation.IntegrationTests.Features.Plans;

/// <summary>
/// T068 — FR-058/SC-009: hai RPC plan chỉ <b>đọc</b> corpus và không được ghi
/// dữ liệu người dùng. Test gọi cả <c>GenerateMealPlan</c> và
/// <c>GenerateWorkoutPlan</c> trên host thật rồi so sánh ảnh chụp
/// <c>recommendation_db</c> trước/sau: <c>kb.chunk</c> không đổi, không bảng
/// nào nằm ngoài schema <c>kb</c>; đồng thời bắt log của host để khẳng định
/// không có <c>user_id</c>, hồ sơ, allergen, limitation hay nội dung kế hoạch.
/// </summary>
public sealed class PlanNoUserDataTests(RecommendationApiFixture fixture)
    : IClassFixture<RecommendationApiFixture>
{
    private const string AllergenMarker = "đậu phộng";
    private const string LimitationMarker = "bệnh tim";
    private const string PlanContentMarker = "Món dùng để dò rò rỉ nội dung kế hoạch";
    private const string ProbeMarker = "plan-no-user-data-log-probe";
    private const int SeededChunkCount = 2;

    // EF Core tạo bảng lịch sử migration ở schema public khi khởi động, trước
    // mọi lời gọi RPC; nó không thuộc dữ liệu nghiệp vụ nên được miễn trừ khỏi
    // phép kiểm tra "mọi bảng phải nằm trong kb".
    private const string MigrationsHistoryTable = "public.__EFMigrationsHistory";

    private static readonly string[] ForbiddenLogFragments =
    [
        "user_id",
        "userid",
        "profile",
        "allerg",
        "limitation",
        AllergenMarker,
        LimitationMarker,
        PlanContentMarker,
    ];

    [Fact]
    public async Task PlanRpcs_LeaveKnowledgeRowsAndTablesUnchanged()
    {
        await SeedCorpusAsync();

        var before = await ReadDatabaseSnapshotAsync();

        await fixture.Client.GenerateMealPlanAsync(BuildMealPlanRequest());
        await fixture.Client.GenerateWorkoutPlanAsync(BuildWorkoutPlanRequest());

        var after = await ReadDatabaseSnapshotAsync();

        Assert.Equal(SeededChunkCount, before.ChunkCount);
        Assert.Equal(before.ChunkCount, after.ChunkCount);
        Assert.Equal(before.Tables, after.Tables);
        Assert.True(
            after.Tables.Count > 0,
            "Không đọc được bảng nào của recommendation_db.");
        Assert.All(
            after.Tables.Where(table => table != MigrationsHistoryTable),
            table => Assert.True(
                table.StartsWith("kb.", StringComparison.Ordinal),
                $"Bảng {table} nằm ngoài schema kb của recommendation_db."));
    }

    [Fact]
    public async Task PlanRpcs_LogNoUserIdentityAllergenLimitationOrPlanContent()
    {
        await SeedCorpusAsync();

        var loggerFactory = fixture.Services.GetRequiredService<ILoggerFactory>();
        var factory = loggerFactory as LoggerFactory
            ?? throw new InvalidOperationException(
                "ILoggerFactory của host test không phải LoggerFactory nên không capture được log.");

        var capture = new CapturingLoggerProvider();
        factory.AddProvider(capture);

        loggerFactory
            .CreateLogger("PlanNoUserDataTests.Probe")
            .LogInformation("Log capture probe {Marker}", ProbeMarker);

        Assert.Contains(capture.Entries, entry => entry.Contains(ProbeMarker, StringComparison.Ordinal));

        var baseline = capture.Entries.Count;

        await fixture.Client.GenerateMealPlanAsync(BuildMealPlanRequest());
        await fixture.Client.GenerateWorkoutPlanAsync(BuildWorkoutPlanRequest());

        var leaked = capture.Entries
            .Skip(baseline)
            .Where(entry => ForbiddenLogFragments.Any(
                fragment => entry.Contains(fragment, StringComparison.OrdinalIgnoreCase)))
            .ToArray();

        Assert.True(
            leaked.Length == 0,
            "Log của host chứa dữ liệu người dùng/nội dung kế hoạch: " + string.Join(" | ", leaked));
    }

    private async Task SeedCorpusAsync()
    {
        await CorpusSeeder.SeedAsync(
            fixture.Services,
            [
                new SeedChunk
                {
                    Content = "Chế độ ăn trường thọ ưu tiên rau xanh, đậu và ngũ cốc nguyên hạt.",
                    BookPageFrom = 72,
                    PdfPageFrom = 64,
                    Verified = true,
                },
                new SeedChunk
                {
                    Content = "Bữa sáng cân bằng nên có protein thực vật và chất xơ.",
                    BookPageFrom = 73,
                    PdfPageFrom = 65,
                    Verified = true,
                },
            ],
            CancellationToken.None);
    }

    private async Task<DatabaseSnapshot> ReadDatabaseSnapshotAsync()
    {
        var connectionString = fixture.Services
            .GetRequiredService<IConfiguration>()
            .GetConnectionString("Recommendation");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Fixture chưa cấu hình ConnectionStrings:Recommendation.");
        }

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        var tables = new List<string>();
        await using (var tablesCommand = new NpgsqlCommand(
            """
            SELECT table_schema || '.' || table_name
            FROM information_schema.tables
            WHERE table_type = 'BASE TABLE'
              AND table_schema NOT IN ('pg_catalog', 'information_schema')
              AND table_name NOT LIKE 'pg\_%'
            ORDER BY table_schema, table_name;
            """,
            connection))
        await using (var reader = await tablesCommand.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                tables.Add(reader.GetString(0));
            }
        }

        await using var countCommand = new NpgsqlCommand("SELECT count(*) FROM kb.chunk;", connection);
        var chunkCount = (long)(await countCommand.ExecuteScalarAsync()
            ?? throw new InvalidOperationException("Không đếm được kb.chunk."));

        return new DatabaseSnapshot(chunkCount, tables);
    }

    private static GenerateMealPlanRequest BuildMealPlanRequest()
    {
        var request = new GenerateMealPlanRequest
        {
            Profile = new UserProfile
            {
                Age = 35,
                Sex = BiologicalSex.Female,
                HeightCm = 165,
                WeightKg = 60,
                ActivityLevel = "ModeratelyActive",
                DietaryPreference = "None",
            },
            TargetCalories = 2000,
            TargetProteinGrams = 100,
            TargetCarbsGrams = 250,
            TargetFatGrams = 67,
            TargetFiberGrams = 30,
            WeekStartDate = "2026-10-26",
            Days = 7,
            MealsPerDay = 4,
            Locale = "vi-VN",
        };

        request.Profile.Allergies.Add(AllergenMarker);

        request.CandidateFoods.AddRange(
        [
            Food("f-marker", PlanContentMarker, 500, 25, 62.5, 16.75),
            Food("f-meal-breakfast", "Bữa sáng cân bằng", 500, 25, 62.5, 16.75),
            Food("f-meal-lunch", "Bữa trưa cân bằng", 700, 35, 87.5, 23.45),
            Food("f-meal-dinner", "Bữa tối cân bằng", 600, 30, 75, 20.1),
            Food("f-meal-snack", "Bữa phụ cân bằng", 200, 10, 25, 6.7),
            Food("f-water", "Nước lọc", 0, 0, 0, 0),
            Food("f-rice", "Cơm gạo lứt", 230, 5, 45, 2),
            Food("f-tofu", "Đậu hũ sốt cà", 180, 14, 8, 9),
        ]);

        return request;
    }

    private static GenerateWorkoutPlanRequest BuildWorkoutPlanRequest()
    {
        var request = new GenerateWorkoutPlanRequest
        {
            Profile = new UserProfile
            {
                Age = 35,
                Sex = BiologicalSex.Female,
                HeightCm = 165,
                WeightKg = 60,
                ActivityLevel = "ModeratelyActive",
                DietaryPreference = "None",
            },
            Goal = UserGoal.Longevity,
            WeekStartDate = "2026-10-26",
            Days = 7,
            SessionsPerWeek = 3,
            MinutesPerSession = 30,
            Locale = "vi-VN",
        };

        request.ActivityPreference.Add("walking");
        request.Limitations.Add(LimitationMarker);

        return request;
    }

    private static CandidateFood Food(
        string foodId,
        string name,
        int calories,
        double protein,
        double carbs,
        double fat)
    {
        return new CandidateFood
        {
            FoodId = foodId,
            Name = name,
            ServingSize = "1",
            ServingUnit = "phần",
            Calories = calories,
            ProteinGrams = protein,
            CarbsGrams = carbs,
            FatGrams = fat,
            FiberGrams = 5,
        };
    }

    private sealed record DatabaseSnapshot(long ChunkCount, IReadOnlyList<string> Tables);

    /// <summary>
    /// Provider chỉ ghi lại log đã format; đủ để dò chuỗi dữ liệu người dùng mà
    /// không phụ thuộc console của tiến trình test.
    /// </summary>
    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        private readonly ConcurrentQueue<string> _entries = new();

        public IReadOnlyList<string> Entries => _entries.ToArray();

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(categoryName, _entries);

        public void Dispose()
        {
        }

        private sealed class CapturingLogger(
            string categoryName,
            ConcurrentQueue<string> entries) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                ArgumentNullException.ThrowIfNull(formatter);

                entries.Enqueue($"{logLevel}:{categoryName}:{formatter(state, exception)}");

                if (exception is not null)
                {
                    entries.Enqueue($"{logLevel}:{categoryName}:exception:{exception}");
                }
            }
        }
    }
}
