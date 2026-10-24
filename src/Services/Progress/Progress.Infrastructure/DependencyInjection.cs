using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Progress.Application.Abstractions;
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
        services.AddScoped<IBodyProfileProvider, Adapters.Grpc.BodyProfileProvider>();
        services.AddScoped<IActivityContributionRepository, Repositories.ActivityContributionRepository>();
        return services;
    }
}
