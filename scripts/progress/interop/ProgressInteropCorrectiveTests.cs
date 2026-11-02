using System.Net.Http.Json;
using Diet.Application.Features.Meals;
using Diet.Domain.Entities;
using Diet.Infrastructure.Messaging;
using Diet.Infrastructure.Persistence;
using DotNet.Testcontainers.Builders;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Progress.Infrastructure.Messaging;
using Progress.Infrastructure.Persistence;
using Progress.Infrastructure.Reconciliation;
using Testcontainers.PostgreSql;

namespace Diet.IntegrationTests;

// Isolated operator evidence against unmodified Diet PR12; never shipped in PR13.
public sealed class ProgressInteropCorrectiveTests
{
    [Fact]
    public async Task ActualDietProducer_ReplayAndSnapshot_ReconcileActualProgress()
    {
        await using var rabbit = new ContainerBuilder("rabbitmq:4-management-alpine")
            .WithEnvironment("RABBITMQ_DEFAULT_USER", "diet_test")
            .WithEnvironment("RABBITMQ_DEFAULT_PASS", "diet_test_password")
            .WithPortBinding(5672, true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilMessageIsLogged("Server startup complete")).Build();
        await using var postgres = new PostgreSqlBuilder("postgres:17-alpine")
            .WithDatabase("progress_db").WithUsername("progress_user").WithPassword(Guid.NewGuid().ToString("N")).Build();
        await rabbit.StartAsync(); await postgres.StartAsync();
        await using var progressDb = new ProgressDbContext(new DbContextOptionsBuilder<ProgressDbContext>().UseNpgsql(postgres.GetConnectionString()).Options);
        await progressDb.Database.MigrateAsync();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["RabbitMQ:Host"] = rabbit.Hostname, ["RabbitMQ:Port"] = rabbit.GetMappedPublicPort(5672).ToString(),
            ["RabbitMQ:User"] = "diet_test", ["RabbitMQ:Password"] = "diet_test_password"
        }).Build();
        var services = new ServiceCollection();
        services.AddLogging(); services.AddSingleton(TimeProvider.System);
        services.AddDbContext<ProgressDbContext>(o => o.UseNpgsql(postgres.GetConnectionString()));
        services.AddProgressMessaging(config);
        await using var provider = services.BuildServiceProvider();
        var bus = provider.GetRequiredService<IBusControl>();
        var observer = new RoutingObserver();
        bus.ConnectConsumeObserver(observer);
        await bus.StartAsync();
        var factory = new DietApiFactory { RabbitHost = rabbit.Hostname, RabbitPort = rabbit.GetMappedPublicPort(5672) };
        await factory.InitializeAsync();
        var path = Path.GetTempFileName(); File.Delete(path);
        try
        {
            var user = Guid.NewGuid();
            using var client = factory.UserClient(user);
            var date = new DateOnly(2026, 1, 15);
            var response = await client.PostAsJsonAsync("/meals", new LogMealRequest(date, "Lunch",
                [new(Guid.Parse("11111111-1111-4111-8111-111111111111"), 100)]));
            Assert.Equal(System.Net.HttpStatusCode.Created, response.StatusCode);
            var meal = (await response.Content.ReadFromJsonAsync<MealResponse>())!;
            await EventuallyAsync(async () => await progressDb.ProcessedMealEvents.AnyAsync(x => x.EventId == meal.Id));
            Assert.Equal(date, (await progressDb.ActivityContributions.SingleAsync(x => x.SourceLogId == meal.Id.ToString())).Date);
            Assert.Equal(("longevia.events", "diet.meal.logged"), observer.Routes[meal.Id]);
            await using var scope = factory.Services.CreateAsyncScope();
            var dietDb = scope.ServiceProvider.GetRequiredService<DietDbContext>();
            var historical = Enumerable.Range(0, 101).Select(_ => MealLog.Create(user, date.AddDays(-1), "Breakfast", DateTimeOffset.UtcNow)).ToArray();
            dietDb.MealLogs.AddRange(historical); await dietDb.SaveChangesAsync();
            Assert.Equal(102, await scope.ServiceProvider.GetRequiredService<MealActivityReplay>().EnqueueAsync());
            await EventuallyAsync(async () => await progressDb.ProcessedMealEvents.CountAsync() == 102);
            Assert.Equal(102, await scope.ServiceProvider.GetRequiredService<MealActivitySnapshotExporter>().ExportAsync(path));
            using var report = new StringWriter();
            Assert.Equal(0, await MealSnapshotReconciler.RunAsync(path, postgres.GetConnectionString(), report));
            Assert.Contains("Matched: 102;", report.ToString());
            Assert.Contains("PASS:", report.ToString());
            Console.WriteLine("ACTUAL PR12 producer -> RabbitMQ -> PR13 consumer -> PostgreSQL: 102 inbox/contributions; replay + JSON snapshot reconciliation PASS.");
        }
        finally { File.Delete(path); await factory.DisposeAsync(); await bus.StopAsync(); }
    }

    private static async Task EventuallyAsync(Func<Task<bool>> check)
    {
        var deadline = DateTime.UtcNow.AddSeconds(40);
        while (DateTime.UtcNow < deadline) { if (await check()) return; await Task.Delay(100); }
        Assert.Fail("Actual Diet publisher did not deliver to canonical Progress topic binding in 40s.");
    }

    private sealed class RoutingObserver : IConsumeObserver
    {
        public System.Collections.Concurrent.ConcurrentDictionary<Guid, (string, string)> Routes { get; } = new();
        public Task PreConsume<T>(ConsumeContext<T> context) where T : class
        {
            if (context.Message is Longevia.Shared.Events.MealLoggedEvent message
                && context.ReceiveContext.TryGetPayload<RabbitMqBasicConsumeContext>(out var rabbit))
                Routes[message.EventId] = (rabbit.Exchange, rabbit.RoutingKey ?? "");
            return Task.CompletedTask;
        }
        public Task PostConsume<T>(ConsumeContext<T> context) where T : class => Task.CompletedTask;
        public Task ConsumeFault<T>(ConsumeContext<T> context, Exception exception) where T : class => Task.CompletedTask;
    }
}
