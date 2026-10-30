using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Progress.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace Progress.IntegrationTests.Metrics;

// Starts the actual Identity entry point; no Identity project/source/DB reference from Progress.
public sealed class IdentityHeightFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _identity = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("identity_db").WithUsername("identity_user").WithPassword(Guid.NewGuid().ToString("N")).Build();
    private Process? _process;
    private readonly int _httpPort = FreePort();
    private readonly int _grpcPort = FreePort();
    public ProgressFixture Progress { get; } = new();
    public HttpClient IdentityHttp { get; private set; } = null!;
    public string Address => $"http://127.0.0.1:{_grpcPort}";

    public async Task InitializeAsync()
    {
        await _identity.StartAsync();
        await Progress.InitializeAsync();
        IdentityHttp = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{_httpPort}") };
        await StartIdentityAsync();
    }

    public async Task StartIdentityAsync()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "longevia-be.slnx"))) root = root.Parent;
        if (root is null) throw new InvalidOperationException("Repository root not found.");
        var start = new ProcessStartInfo("dotnet")
        { WorkingDirectory = root.FullName, RedirectStandardOutput = true, RedirectStandardError = true };
#if DEBUG
        const string configuration = "Debug";
#else
        const string configuration = "Release";
#endif
        foreach (var arg in new[] { "run", "--project", "src/Services/Identity/Identity.Api/Identity.Api.csproj", "--no-launch-profile", "--no-restore", "--configuration", configuration })
            start.ArgumentList.Add(arg);
        foreach (var pair in new Dictionary<string, string>
        {
            ["ConnectionStrings__Identity"] = _identity.GetConnectionString(),
            ["IDENTITY__HTTP_PORT"] = _httpPort.ToString(),
            ["IDENTITY__GRPC_PORT"] = _grpcPort.ToString(),
            ["RABBITMQ_HOST"] = "",
            ["RabbitMQ__Host"] = "",
            ["Jwt__Issuer"] = "progress-tests",
            ["Jwt__Audience"] = "progress-test-clients",
            ["Jwt__KeyId"] = "current",
            ["Jwt__SigningPublicKey"] = ProgressFixture.PublicKey(Progress.CurrentKey),
            ["Jwt__SigningPrivateKey"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(Progress.CurrentKey.ExportPkcs8PrivateKeyPem())),
            ["Jwt__PreviousPublicKey"] = "",
            ["Jwt__PreviousKeyId"] = "",
            ["ASPNETCORE_ENVIRONMENT"] = "Testing",
            ["Logging__LogLevel__Default"] = "Warning"
        }) start.Environment[pair.Key] = pair.Value;
        _process = Process.Start(start)!;
        var stdout = _process.StandardOutput.ReadToEndAsync();
        var stderr = _process.StandardError.ReadToEndAsync();
        var deadline = DateTime.UtcNow.AddSeconds(90);
        while (DateTime.UtcNow < deadline)
        {
            if (_process.HasExited)
            {
                var output = await stdout + await stderr;
                // Report only exception types/resource markers, never provider configuration or raw logs.
                var types = System.Text.RegularExpressions.Regex.Matches(output, @"[A-Za-z0-9_.]+Exception")
                    .Select(x => x.Value).Distinct();
                var memoryFailure = output.Contains("Out of memory", StringComparison.OrdinalIgnoreCase) ||
                    output.Contains("Failed to create CoreCLR", StringComparison.OrdinalIgnoreCase);
                throw new InvalidOperationException($"Actual Identity process exited: {_process.ExitCode}; exception types: {string.Join(", ", types)}; runtime memory failure: {memoryFailure}.");
            }
            try { if ((await IdentityHttp.GetAsync("/health")).IsSuccessStatusCode) return; }
            catch (HttpRequestException) { }
            await Task.Delay(100);
        }
        throw new TimeoutException("Actual Identity HTTP/1 and HTTP/2 host did not start within 90s.");
    }

    public async Task StopIdentityAsync()
    {
        if (_process is not null)
        {
            if (!_process.HasExited) { _process.Kill(entireProcessTree: true); await _process.WaitForExitAsync(); }
            _process.Dispose(); _process = null;
        }
    }

    public ProgressApiFactory Factory(string? address = null, int deadlineMs = 2000) => new(Progress,
        new Dictionary<string, string?> { ["IdentityGrpc:Address"] = address ?? Address, ["IdentityGrpc:DeadlineMilliseconds"] = deadlineMs.ToString() });

    public HttpClient Client(ProgressApiFactory factory, Guid user)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestTokens.Create(Progress, "valid", user));
        return client;
    }

    public async Task<Guid> SeedAsync(decimal? height = null)
    {
        using var response = await IdentityHttp.PostAsJsonAsync("/auth/register", new
        { email = $"height-{Guid.NewGuid():N}@example.test", password = "HeightTest!825", fullName = "Height Integration" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var id = body.RootElement.GetProperty("id").GetGuid();
        if (height.HasValue) await UpdateAsync(id, height.Value);
        return id;
    }

    public async Task UpdateAsync(Guid id, decimal height)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, "/users/me/profile");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", TestTokens.Create(Progress, "valid", id));
        request.Content = JsonContent.Create(new
        {
            birthDate = "1990-01-01",
            biologicalSex = "Male",
            heightCm = height,
            activityLevel = "Sedentary",
            dietaryPreference = "None",
            allergies = Array.Empty<string>()
        });
        using var response = await IdentityHttp.SendAsync(request);
        response.EnsureSuccessStatusCode();
    }

    public ProgressDbContext Db() => new(new DbContextOptionsBuilder<ProgressDbContext>().UseNpgsql(Progress.ConnectionString).Options);

    public async Task DisposeAsync()
    {
        await StopIdentityAsync(); IdentityHttp?.Dispose();
        await Progress.DisposeAsync(); await _identity.DisposeAsync();
    }

    private static int FreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start(); return ((IPEndPoint)listener.LocalEndpoint).Port;
    }
}
