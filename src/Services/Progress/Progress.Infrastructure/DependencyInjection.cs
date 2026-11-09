using Grpc.Core.Interceptors;
using Grpc.Net.Client;
using Longevia.Diet.Contracts.V1;
using Longevia.Identity.Contracts.V1;
using Longevia.Recommendation.Contracts.V1;
using Longevia.Shared.Auth;
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
        var serviceToken = config["SERVICE:TOKEN"] ?? string.Empty;
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
        services.AddScoped<IWorkoutPlanRepository, Repositories.WorkoutPlanRepository>();
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
        services.AddSingleton(provider => new IdentityService.IdentityServiceClient(
            provider.GetRequiredService<GrpcChannel>().Intercept(new ServiceTokenInterceptor(serviceToken))));
        services.AddScoped<IBodyProfileProvider, BodyProfileProvider>();
        services.AddScoped<IUserProfileProvider, UserProfileProvider>();
        services.AddScoped<IUserContactProvider, UserContactProvider>();
        services.AddKeyedSingleton<GrpcChannel>("recommendation", (_, _) => GrpcChannel.ForAddress(
            config["RecommendationGrpc:Address"] ?? "http://recommendation:50051"));
        services.AddSingleton(provider => new RecommendationService.RecommendationServiceClient(
            provider.GetRequiredKeyedService<GrpcChannel>("recommendation")
                .Intercept(new ServiceTokenInterceptor(serviceToken))));
        services.AddScoped<IWorkoutPlanProvider, WorkoutPlanProvider>();
        services.AddScoped<ILongevityProvider, LongevityProvider>();
        services.AddKeyedSingleton<GrpcChannel>("diet", (_, _) => GrpcChannel.ForAddress(
            config["DietGrpc:Address"] ?? "http://diet:50051"));
        services.AddSingleton(provider => new DietService.DietServiceClient(
            provider.GetRequiredKeyedService<GrpcChannel>("diet")
                .Intercept(new ServiceTokenInterceptor(serviceToken))));
        services.AddScoped<INutritionAdherenceProvider, NutritionAdherenceProvider>();
        services.AddScoped<IActivityContributionRepository, Repositories.ActivityContributionRepository>();
        services.AddProgressMessaging(config);
        services.AddHostedService<WeeklyDigestScheduler>();
        return services;
    }
}
