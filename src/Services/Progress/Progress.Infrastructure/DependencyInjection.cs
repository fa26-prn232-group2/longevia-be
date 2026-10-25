using Grpc.Net.Client;
using Longevia.Identity.Contracts.V1;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Progress.Application.Abstractions;
using Progress.Infrastructure.Adapters.Grpc;
using Progress.Infrastructure.Messaging;
using Progress.Infrastructure.Persistence;

namespace Progress.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddProgressInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services.AddOptions<ProgressDatabaseOptions>()
            .Configure(options => options.Progress = config.GetConnectionString("Progress"))
            .Validate(options => options.IsValid(),
                "ConnectionStrings:Progress phải trỏ progress_db bằng progress_user, với Host hợp lệ.")
            .ValidateOnStart();
        services.AddDbContext<ProgressDbContext>((provider, options) => options.UseNpgsql(
            provider.GetRequiredService<IOptions<ProgressDatabaseOptions>>().Value.Progress));
        services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<ProgressDbContext>());
        services.AddScoped<IFastingRepository, Repositories.FastingRepository>();
        services.AddScoped<IWaterRepository, Repositories.WaterRepository>();
        services.AddScoped<ISleepRepository, Repositories.SleepRepository>();
        services.AddScoped<IBodyMetricRepository, Repositories.BodyMetricRepository>();
        services.AddOptions<IdentityGrpcOptions>().Bind(config.GetSection("IdentityGrpc"))
            .Validate(options => options.IsValid(), "IdentityGrpc requires an HTTP(S) address and deadline of 50–10000 ms.")
            .ValidateOnStart();
        services.AddSingleton(provider => GrpcChannel.ForAddress(
            provider.GetRequiredService<IOptions<IdentityGrpcOptions>>().Value.Address!,
            new GrpcChannelOptions
            {
                HttpHandler = new SocketsHttpHandler { EnableMultipleHttp2Connections = true },
                DisposeHttpClient = true
            }));
        services.AddSingleton(provider => new IdentityService.IdentityServiceClient(provider.GetRequiredService<GrpcChannel>()));
        services.AddScoped<IBodyProfileProvider, BodyProfileProvider>();
        services.AddScoped<IActivityContributionRepository, Repositories.ActivityContributionRepository>();
        services.AddProgressMessaging(config);
        return services;
    }
}
