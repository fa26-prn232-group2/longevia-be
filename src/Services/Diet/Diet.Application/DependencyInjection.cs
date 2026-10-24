using Diet.Application.Features.Foods;
using Diet.Application.Features.Meals;
using Diet.Application.Features.Targets;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Diet.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddDietApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<FoodUseCases>();
        services.AddScoped<TargetUseCases>();
        services.AddScoped<MealUseCases>();
        services.AddScoped<DailySummaryUseCase>();
        return services;
    }
}
