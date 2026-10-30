using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Diet.Infrastructure.Messaging;

public sealed class MealActivityReplayService(IServiceScopeFactory scopes, IConfiguration config,
    ILogger<MealActivityReplayService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!config.GetValue<bool>("Diet:ReplayMealActivitiesOnStart"))
        {
            return;
        }
        await using var scope = scopes.CreateAsyncScope();
        var count = await scope.ServiceProvider.GetRequiredService<MealActivityReplay>().EnqueueAsync(cancellationToken);
        logger.LogInformation("Meal activity replay: {Count} events queued in outbox; delivery not yet confirmed.", count);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
