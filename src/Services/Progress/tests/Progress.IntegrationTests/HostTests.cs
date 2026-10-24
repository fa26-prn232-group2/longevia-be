using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Progress.Application.Abstractions;
using Progress.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace Progress.IntegrationTests;

[Collection("Progress foundation")]
public sealed class HostTests(ProgressFixture fixture)
{
    [Fact]
    public async Task Host_HealthyDatabase_ReportsLivenessReadinessAndEmptySchema()
    {
        using var client = fixture.Factory.CreateClient();
        foreach (var route in new[] { "/alive", "/health" })
        {
            var result = await client.GetFromJsonAsync<JsonElement>(route);
            Assert.Equal("ok", result.GetProperty("status").GetString());
        }
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ProgressDbContext>();
        Assert.Same(db, scope.ServiceProvider.GetRequiredService<IUnitOfWork>());
        Assert.Empty(db.Model.GetEntityTypes());
        Assert.Empty(db.Database.GetMigrations());
        await ProgressDatabaseInitializer.InitializeAsync(db, CancellationToken.None);
        await ProgressDatabaseInitializer.InitializeAsync(db, CancellationToken.None);
        Assert.Empty(await db.Database.GetAppliedMigrationsAsync());
        Assert.Equal(0, await db.SaveChangesAsync());
        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT current_database(), current_user, (SELECT count(*) FROM information_schema.tables WHERE table_schema = 'public')", connection);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal("progress_db", reader.GetString(0));
        Assert.Equal("progress_user", reader.GetString(1));
        Assert.Equal(0, reader.GetInt64(2));
    }

    [Fact]
    public async Task DatabaseInitializer_CanceledStartup_DoesNotContinue()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var options = new DbContextOptionsBuilder<ProgressDbContext>().UseNpgsql(fixture.ConnectionString).Options;
        await using var db = new ProgressDbContext(options);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            ProgressDatabaseInitializer.InitializeAsync(db, cancellation.Token));
    }

    [Fact]
    public async Task Host_ReadinessDatabaseUnavailable_LivenessRemainsHealthy()
    {
        await using var database = new PostgreSqlBuilder("postgres:17-alpine")
            .WithDatabase("progress_db").WithUsername("progress_user")
            .WithPassword(Guid.NewGuid().ToString("N")).Build();
        await database.StartAsync();
        using var factory = new ProgressApiFactory(fixture, new Dictionary<string, string?>
        {
            ["ConnectionStrings:Progress"] = database.GetConnectionString(),
        });
        using var client = factory.CreateClient();
        using var healthy = await client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, healthy.StatusCode);
        await database.StopAsync();
        using var readiness = await client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, readiness.StatusCode);
        using var alive = await client.GetAsync("/alive");
        Assert.Equal(HttpStatusCode.OK, alive.StatusCode);
    }

    [Theory]
    [InlineData("ConnectionStrings:Progress", "")]
    [InlineData("ConnectionStrings:Progress", "Host=localhost;Database=diet_db;Username=progress_user")]
    [InlineData("Jwt:SigningPublicKey", "")]
    [InlineData("Jwt:SigningPublicKey", "not-base64")]
    [InlineData("Jwt:SigningPrivateKey", "test-private-value")]
    [InlineData("Jwt:Issuer", "")]
    [InlineData("Jwt:Audience", "")]
    public async Task Host_InvalidConfiguration_FailsBeforeServing(string setting, string value)
    {
        await using var factory = new ProgressApiFactory(fixture, new Dictionary<string, string?> { [setting] = value });
        var error = Assert.ThrowsAny<Exception>(() => factory.CreateClient());
        Assert.DoesNotContain("test-private-value", error.ToString());
    }

    [Fact]
    public async Task Host_UnavailableDatabase_FailsStartup()
    {
        await using var factory = new ProgressApiFactory(fixture, new Dictionary<string, string?>
        {
            ["ConnectionStrings:Progress"] = UnavailableConnection(),
        });
        Assert.ThrowsAny<Exception>(() => factory.CreateClient());
    }

    [Fact]
    public async Task Swagger_Development_UsesGatewayBaseAndBearer()
    {
        using var client = fixture.Factory.CreateClient();
        var document = await client.GetFromJsonAsync<JsonElement>("/swagger/v1/swagger.json");
        Assert.Equal("/api/progress", document.GetProperty("servers")[0].GetProperty("url").GetString());
        var bearer = document.GetProperty("components").GetProperty("securitySchemes").GetProperty("Bearer");
        Assert.Equal("bearer", bearer.GetProperty("scheme").GetString());
        var html = await client.GetStringAsync("/swagger/index.html");
        var scripts = Regex.Matches(html, "<script[^>]+src=\"([^\"]+)\"");
        Assert.NotEmpty(scripts);
        var scriptUrl = new Uri(new Uri(client.BaseAddress!, "/swagger/index.html"), scripts[^1].Groups[1].Value);
        var script = await client.GetStringAsync(scriptUrl);
        Assert.Contains("v1/swagger.json", script);
        var paths = document.GetProperty("paths");
        Assert.False(paths.TryGetProperty("/fasting/start", out _));
        Assert.False(paths.TryGetProperty("/metrics/history", out _));
    }

    [Fact]
    public async Task Swagger_Production_IsNotExposed()
    {
        await using var factory = new ProgressApiFactory(fixture, new Dictionary<string, string?>
        {
            ["environment"] = "Production",
        });
        using var client = factory.CreateClient();
        using var result = await client.GetAsync("/swagger/v1/swagger.json");
        Assert.Equal(HttpStatusCode.NotFound, result.StatusCode);
    }

    [Fact]
    public async Task Metrics_HostedExporter_ReturnsPrometheusData()
    {
        using var client = fixture.Factory.CreateClient();
        using var alive = await client.GetAsync("/alive");
        using var result = await client.GetAsync("/metrics");
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        var content = await result.Content.ReadAsStringAsync();
        Assert.Contains("# TYPE", content);
        Assert.Contains("target_info{", content);
        Assert.Contains("service_name=\"ProgressService\"", content);
    }

    [Theory]
    [InlineData("/_test/fault", 500, "system.internal")]
    [InlineData("/_test/conflict", 409, "progress.test_conflict")]
    public async Task ExceptionHandler_Exceptions_ReturnSafeProblemDetails(string path, int status, string code)
    {
        using var client = AuthorizedClient();
        using var result = await client.GetAsync(path);
        Assert.Equal(status, (int)result.StatusCode);
        var content = await result.Content.ReadAsStringAsync();
        Assert.DoesNotContain("test-sensitive-value", content);
        var problem = JsonSerializer.Deserialize<JsonElement>(content);
        Assert.Equal(code, problem.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(problem.GetProperty("traceId").GetString()));
        Assert.Equal("application/problem+json", result.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Validation_InvalidRequest_ReturnsFieldErrors()
    {
        using var client = AuthorizedClient();
        using var result = await client.PostAsJsonAsync("/_test/validate", new ProbeRequest(0));
        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
        var problem = await result.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("validation.failed", problem.GetProperty("code").GetString());
        Assert.Equal("Value", problem.GetProperty("errors")[0].GetProperty("field").GetString());
        using var valid = await client.PostAsJsonAsync("/_test/validate", new ProbeRequest(42));
        Assert.Equal(42, (await valid.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("value").GetInt32());
    }

    private HttpClient AuthorizedClient()
    {
        var client = fixture.Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            TestTokens.Create(fixture, "valid", Guid.NewGuid()));
        return client;
    }

    private string UnavailableConnection()
    {
        var builder = new NpgsqlConnectionStringBuilder(fixture.ConnectionString)
        {
            Port = 1,
            Timeout = 1,
            Pooling = false,
        };
        return builder.ConnectionString;
    }
}
