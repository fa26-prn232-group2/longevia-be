using System.Net;
using System.Net.Sockets;
using Grpc.Core;
using Grpc.Net.Client;
using Identity.Api.Endpoints;
using Identity.Domain.Constants;
using Identity.Domain.Entities;
using Identity.Domain.Repositories;
using Identity.Infrastructure.Persistence;
using Identity.Infrastructure.Persistence.Repositories;
using Longevia.Identity.Contracts.V1;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace Identity.IntegrationTests;

public sealed class IdentityGrpcTests(IdentityGrpcApiFixture fixture) : IClassFixture<IdentityGrpcApiFixture>
{
    [Fact]
    public async Task GetUserHeight_ProfileExists_ReturnsHeightScaledByOneHundred()
    {
        var userId = await SeedUserAsync(heightCm: 175.25m);

        var response = await fixture.Client.GetUserHeightAsync(
            new GetUserHeightRequest { UserId = userId.ToString() },
            deadline: DateTime.UtcNow.AddSeconds(5)).ResponseAsync;

        Assert.True(response.HasHeightCmX100);
        Assert.Equal(17525, response.HeightCmX100);
    }

    [Fact]
    public async Task GetUserHeight_UserDoesNotExist_ReturnsNotFound()
    {
        var exception = await Assert.ThrowsAsync<RpcException>(async () =>
            await fixture.Client.GetUserHeightAsync(
                new GetUserHeightRequest { UserId = Guid.NewGuid().ToString() },
                deadline: DateTime.UtcNow.AddSeconds(5)).ResponseAsync);

        Assert.Equal(StatusCode.NotFound, exception.StatusCode);
    }

    [Fact]
    public async Task GetUserHeight_ProfileDoesNotExist_ReturnsNotFound()
    {
        var userId = await SeedUserAsync();

        var exception = await Assert.ThrowsAsync<RpcException>(async () =>
            await fixture.Client.GetUserHeightAsync(
                new GetUserHeightRequest { UserId = userId.ToString() },
                deadline: DateTime.UtcNow.AddSeconds(5)).ResponseAsync);

        Assert.Equal(StatusCode.NotFound, exception.StatusCode);
    }

    [Fact]
    public async Task GetUserHeight_PersistedHeightOutsideDomainRange_ReturnsInternal()
    {
        var userId = await SeedUserAsync(heightCm: 175m);
        using (var seedScope = fixture.Services.CreateScope())
        {
            var db = seedScope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE user_profiles SET \"HeightCm\" = {251m} WHERE \"UserId\" = {userId}");
        }

        var exception = await Assert.ThrowsAsync<RpcException>(async () =>
            await fixture.Client.GetUserHeightAsync(
                new GetUserHeightRequest { UserId = userId.ToString() },
                deadline: DateTime.UtcNow.AddSeconds(5)).ResponseAsync);

        Assert.Equal(StatusCode.Internal, exception.StatusCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-guid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public async Task GetUserHeight_InvalidUserId_ReturnsInvalidArgument(string userId)
    {
        var exception = await Assert.ThrowsAsync<RpcException>(async () =>
            await fixture.Client.GetUserHeightAsync(
                new GetUserHeightRequest { UserId = userId },
                deadline: DateTime.UtcNow.AddSeconds(5)).ResponseAsync);

        Assert.Equal(StatusCode.InvalidArgument, exception.StatusCode);
    }

    private async Task<Guid> SeedUserAsync(decimal? heightCm = null)
    {
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var userId = Guid.NewGuid();
        var token = Guid.NewGuid().ToString("N");
        var user = User.Create(
            userId,
            $"grpc_{userId:N}@example.com",
            "existing-password-hash",
            "gRPC Integration User",
            DateTimeOffset.UtcNow,
            token,
            DateTimeOffset.UtcNow.AddHours(24));
        db.Users.Add(user);

        if (heightCm.HasValue)
        {
            db.UserProfiles.Add(UserProfile.Create(
                Guid.NewGuid(),
                userId,
                new DateOnly(1990, 1, 1),
                BiologicalSex.Male,
                heightCm.Value,
                ActivityLevel.Sedentary,
                DietaryPreference.None,
                [],
                DateTimeOffset.UtcNow));
        }

        await db.SaveChangesAsync();
        return userId;
    }
}

public sealed class IdentityGrpcApiFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("identity_grpc_test")
        .WithUsername("test_user")
        .WithPassword("test_password")
        .Build();

    private WebApplication? _app;
    private GrpcChannel? _channel;

    public IdentityService.IdentityServiceClient Client { get; private set; } = null!;
    public IServiceProvider Services => _app?.Services ?? throw new InvalidOperationException("Fixture chưa khởi động.");

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        var grpcPort = GetFreePort();
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Testing",
            ContentRootPath = AppContext.BaseDirectory,
        });

        builder.WebHost.ConfigureKestrel(options =>
            options.Listen(IPAddress.Loopback, grpcPort, listen => listen.Protocols = HttpProtocols.Http2));
        builder.Services.AddGrpc();
        builder.Services.AddDbContext<IdentityDbContext>(options =>
            options.UseNpgsql(_postgres.GetConnectionString()));
        builder.Services.AddScoped<IUserRepository, UserRepository>();

        _app = builder.Build();
        await using (var scope = _app.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            await db.Database.MigrateAsync();
        }

        _app.MapGrpcService<IdentityGrpcService>();
        await _app.StartAsync();

        _channel = GrpcChannel.ForAddress(
            $"http://127.0.0.1:{grpcPort.ToString(System.Globalization.CultureInfo.InvariantCulture)}",
            new GrpcChannelOptions
            {
                HttpHandler = new SocketsHttpHandler { EnableMultipleHttp2Connections = true },
            });
        Client = new IdentityService.IdentityServiceClient(_channel);
    }

    public async Task DisposeAsync()
    {
        _channel?.Dispose();
        if (_app is not null)
        {
            await _app.StopAsync();
            await _app.DisposeAsync();
        }

        await _postgres.DisposeAsync();
    }

    private static int GetFreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }
}
