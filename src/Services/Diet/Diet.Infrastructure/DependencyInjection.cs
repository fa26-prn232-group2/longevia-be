using Diet.Application.Abstractions;
using Diet.Infrastructure.Messaging;
using Diet.Infrastructure.Persistence;
using Diet.Infrastructure.Persistence.Repositories;
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
        services.AddDietMessaging(config);
        return services;
    }
}
