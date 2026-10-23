using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Identity.Application.Features.Auth;
using Identity.Application.Features.Registration;

namespace Identity.IntegrationTests;

public sealed class RegistrationEmailE2ETests : IAsyncLifetime
{
    private const string RabbitUser = "identity_e2e";
    private const string RabbitPassword = "identity_e2e_password";

    private readonly IContainer _rabbit = new ContainerBuilder("rabbitmq:4-management")
        .WithEnvironment("RABBITMQ_DEFAULT_USER", RabbitUser)
        .WithEnvironment("RABBITMQ_DEFAULT_PASS", RabbitPassword)
        .WithPortBinding(5672, true)
        .WithPortBinding(15672, true)
        .Build();

    private readonly IContainer _redis = new ContainerBuilder("redis:7-alpine")
        .WithPortBinding(6379, true)
        .Build();

    private readonly IContainer _mailpit = new ContainerBuilder("axllent/mailpit:latest")
        .WithPortBinding(1025, true)
        .WithPortBinding(8025, true)
        .Build();

    private IdentityApiFactory? _identityFactory;
    private HttpClient? _identityClient;
    private Process? _workerProcess;
    private HttpClient? _mailpitClient;
    private int _rabbitPort;
    private int _rabbitManagementPort;
    private int _redisPort;
    private int _mailpitSmtpPort;
    private int _mailpitHttpPort;

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_rabbit.StartAsync(), _redis.StartAsync(), _mailpit.StartAsync());
        _rabbitPort = _rabbit.GetMappedPublicPort(5672);
        _rabbitManagementPort = _rabbit.GetMappedPublicPort(15672);
        _redisPort = _redis.GetMappedPublicPort(6379);
        _mailpitSmtpPort = _mailpit.GetMappedPublicPort(1025);
        _mailpitHttpPort = _mailpit.GetMappedPublicPort(8025);

        await WaitForMailpitAsync();
        await WaitForRedisAsync();
        StartWorker();
        await WaitForWorkerQueueAsync();

        _identityFactory = new IdentityApiFactory
        {
            RabbitHost = "127.0.0.1",
            RabbitPort = _rabbitPort,
            RabbitUser = RabbitUser,
            RabbitPassword = RabbitPassword,
        };
        await _identityFactory.InitializeAsync();
        _identityClient = _identityFactory.CreateClient();
    }

    public async Task DisposeAsync()
    {
        _identityClient?.Dispose();
        if (_identityFactory is not null)
        {
            await _identityFactory.DisposeAsync();
        }

        if (_workerProcess is { HasExited: false })
        {
            _workerProcess.Kill(entireProcessTree: true);
            await _workerProcess.WaitForExitAsync();
        }

        _workerProcess?.Dispose();
        _mailpitClient?.Dispose();
        await Task.WhenAll(_rabbit.DisposeAsync().AsTask(), _redis.DisposeAsync().AsTask(), _mailpit.DisposeAsync().AsTask());
    }

    [Fact]
    public async Task Register_ShouldSendVerificationEmailThroughRabbitWorkerAndMailpit_ThenVerifyAccount()
    {
        var email = $"identity_e2e_{Guid.NewGuid():N}@example.com";
        const string password = "SecurePassword123!";
        var response = await _identityClient!.PostAsJsonAsync(
            "/auth/register",
            new RegisterUserRequest(email, password, "Identity E2E"));

        Assert.Equal(System.Net.HttpStatusCode.Created, response.StatusCode);

        var message = await WaitForEmailAsync(email);
        Assert.Contains(email, message.GetProperty("To").ToString(), StringComparison.OrdinalIgnoreCase);
        var html = message.GetProperty("HTML").GetString() ?? string.Empty;
        Assert.Contains("Verify", html, StringComparison.OrdinalIgnoreCase);
        var tokenMatch = Regex.Match(html, "[?&]token=([A-Fa-f0-9]{64})", RegexOptions.CultureInvariant);
        Assert.True(tokenMatch.Success, "The captured verification email must contain a 64-character token link.");

        var verifyResponse = await _identityClient!.GetAsync("/auth/verify?token=" + Uri.EscapeDataString(tokenMatch.Groups[1].Value));
        Assert.Equal(System.Net.HttpStatusCode.NoContent, verifyResponse.StatusCode);

        var loginResponse = await _identityClient!.PostAsJsonAsync("/auth/login", new LoginRequest(email, password));
        Assert.Equal(System.Net.HttpStatusCode.OK, loginResponse.StatusCode);
    }

    private void StartWorker()
    {
        var repositoryRoot = FindRepositoryRoot();
        var testConfiguration = Directory.GetParent(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar))?.Name
            ?? throw new InvalidOperationException($"Could not determine the test build configuration from {AppContext.BaseDirectory}.");
        var workerAssembly = Path.Combine(repositoryRoot, "src", "Services", "Worker", "bin", testConfiguration, "net10.0", "Worker.dll");
        Assert.True(File.Exists(workerAssembly), $"Worker test dependency was not built for {testConfiguration}: {workerAssembly}");

        var startInfo = new ProcessStartInfo("dotnet")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        startInfo.ArgumentList.Add(workerAssembly);
        startInfo.Environment["RABBITMQ_HOST"] = "127.0.0.1";
        startInfo.Environment["RABBITMQ_PORT"] = _rabbitPort.ToString();
        startInfo.Environment["RABBITMQ_USER"] = RabbitUser;
        startInfo.Environment["RABBITMQ_PASSWORD"] = RabbitPassword;
        startInfo.Environment["REDIS_HOST"] = "127.0.0.1";
        startInfo.Environment["REDIS_PORT"] = _redisPort.ToString();
        startInfo.Environment["SMTP__HOST"] = "127.0.0.1";
        startInfo.Environment["SMTP__PORT"] = _mailpitSmtpPort.ToString();
        startInfo.Environment["SMTP__USE_SSL"] = "false";
        startInfo.Environment["SMTP__SENDER_EMAIL"] = "no-reply@longevia.local";
        startInfo.Environment["ASPNETCORE_URLS"] = "http://127.0.0.1:0";

        _workerProcess = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Could not start the Worker process for the email E2E test.");
    }

    private async Task WaitForWorkerQueueAsync()
    {
        using var client = new HttpClient();
        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{RabbitUser}:{RabbitPassword}"));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        var queueUri = $"http://127.0.0.1:{_rabbitManagementPort}/api/queues/%2F/worker.email";

        await WaitUntilAsync(async () =>
        {
            if (_workerProcess?.HasExited == true)
            {
                throw new InvalidOperationException("Worker exited before declaring its RabbitMQ queue.");
            }

            try
            {
                using var response = await client.GetAsync(queueUri);
                return response.IsSuccessStatusCode;
            }
            catch (HttpRequestException)
            {
                return false;
            }
        }, TimeSpan.FromSeconds(45), "Worker did not declare worker.email on RabbitMQ.");
    }

    private async Task WaitForMailpitAsync()
    {
        _mailpitClient = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{_mailpitHttpPort}/") };
        await WaitUntilAsync(async () =>
        {
            try
            {
                using var response = await _mailpitClient!.GetAsync("api/v1/info");
                return response.IsSuccessStatusCode;
            }
            catch (HttpRequestException)
            {
                return false;
            }
        }, TimeSpan.FromSeconds(45), "Mailpit HTTP API did not become ready.");
    }

    private async Task WaitForRedisAsync()
    {
        await WaitUntilAsync(async () =>
        {
            try
            {
                using var tcpClient = new System.Net.Sockets.TcpClient();
                await tcpClient.ConnectAsync("127.0.0.1", _redisPort);
                return tcpClient.Connected;
            }
            catch (SocketException)
            {
                return false;
            }
        }, TimeSpan.FromSeconds(45), "Redis did not become ready.");
    }

    private async Task<JsonElement> WaitForEmailAsync(string email)
    {
        JsonElement message = default;
        await WaitUntilAsync(async () =>
        {
            var searchUri = "api/v1/search?query=" + Uri.EscapeDataString($"to:{email}");
            using var response = await _mailpitClient!.GetAsync(searchUri);
            if (!response.IsSuccessStatusCode)
            {
                return false;
            }

            using var searchDocument = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var messages = searchDocument.RootElement.GetProperty("messages");
            if (messages.GetArrayLength() == 0)
            {
                return false;
            }

            var messageId = messages[0].GetProperty("ID").GetString();
            using var messageResponse = await _mailpitClient.GetAsync($"api/v1/message/{Uri.EscapeDataString(messageId!)}");
            if (!messageResponse.IsSuccessStatusCode)
            {
                return false;
            }

            using var messageDocument = JsonDocument.Parse(await messageResponse.Content.ReadAsStringAsync());
            message = messageDocument.RootElement.Clone();
            return true;
        }, TimeSpan.FromSeconds(45), "Worker did not deliver the verification email to Mailpit.");

        return message;
    }

    private static async Task WaitUntilAsync(Func<Task<bool>> condition, TimeSpan timeout, string failureMessage)
    {
        var deadline = DateTimeOffset.UtcNow.Add(timeout);
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (await condition())
            {
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(250));
        }

        throw new TimeoutException(failureMessage);
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Makefile")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not locate repository root from the integration test output directory.");
    }
}
