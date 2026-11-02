using Diet.Application.Abstractions;
using Diet.Infrastructure.Grpc;
using Diet.Infrastructure.Messaging;
using Diet.Infrastructure.Persistence;
using Diet.Infrastructure.Persistence.Repositories;
using Grpc.Net.Client;
using Longevia.Progress.Contracts.V1;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Diet.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddDietInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        var connection = config.GetConnectionString("Diet")
            ?? throw new InvalidOperationException("Thiếu ConnectionStrings: Diet.");
        services.AddDbContext<DietDbContext>(options => options.UseNpgsql(connection));
        services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<DietDbContext>());
        services.AddScoped<IFoodRepository, FoodRepository>();
        services.AddScoped<ITargetRepository, TargetRepository>();
        services.AddScoped<IMealRepository, MealRepository>();
        services.AddSingleton(_ =>
        {
            var address = config["ProgressGrpc:Address"] ?? "http://progress:50051";
            if (!Uri.TryCreate(address, UriKind.Absolute, out var uri) ||
                (uri.Scheme != "http" && uri.Scheme != "https"))
                throw new InvalidOperationException("Invalid ProgressGrpc:Address.");
            return GrpcChannel.ForAddress(uri);
        });
        services.AddSingleton(provider => new ProgressService.ProgressServiceClient(
            provider.GetRequiredService<GrpcChannel>()));
        services.AddScoped<IWeightProvider, ProgressWeightProvider>();
        services.AddKeyedSingleton<GrpcChannel>("recommendation", (_, _) =>
            GrpcChannel.ForAddress(config["RecommendationGrpc:Address"] ?? "http://recommendation:50051"));
        services.AddSingleton(provider => new Longevia.Recommendation.Contracts.V1.RecommendationService.RecommendationServiceClient(
            provider.GetRequiredKeyedService<GrpcChannel>("recommendation")));
        services.AddScoped<IMealRecommendationProvider, MealRecommendationProvider>();
        services.AddKeyedSingleton<GrpcChannel>("identity", (_, _) =>
            GrpcChannel.ForAddress(config["IdentityGrpc:Address"] ?? "http://identity:50051"));
        services.AddSingleton(provider => new Longevia.Identity.Contracts.V1.IdentityService.IdentityServiceClient(
            provider.GetRequiredKeyedService<GrpcChannel>("identity")));
        services.AddScoped<IProfileProvider, IdentityProfileProvider>();
        services.AddDietMessaging(config);
        return services;
    }
}
