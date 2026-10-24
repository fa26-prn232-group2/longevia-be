using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Progress.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddProgressApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);
        services.AddScoped<Features.Fasting.FastingUseCases>();
        services.AddScoped<Features.Water.WaterUseCases>();
        services.AddScoped<Features.Sleep.SleepUseCases>();
        services.AddScoped<Features.Metrics.MetricUseCases>();
        services.AddScoped<Features.Metrics.IWeightQueryService, Features.Metrics.WeightQueryService>();
        services.AddScoped<Features.Dashboard.DashboardUseCases>();
        services.TryAddSingleton(TimeProvider.System);
        return services;
    }
}
