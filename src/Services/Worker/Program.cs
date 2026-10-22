// Worker host (Phase 5, T018) — Web SDK (see Worker.csproj for the SDK decision).
// A MassTransit bus runs as an IHostedService inside this WebApplication, so the
// process both consumes RabbitMQ events and serves HTTP /metrics on 8080 for
// Prometheus scraping (Phase 6). Kestrel ports come from compose/Dockerfile
// (ASPNETCORE_HTTP_PORTS), never from code.
using Longevia.Shared.Events;
using Longevia.Shared.Observability;
using Longevia.Worker.Adapters.Email;
using Longevia.Worker.Consumers;
using Longevia.Worker.Idempotency;
using Longevia.Worker.Messaging;
using MassTransit;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// -- SMTP (Mailpit local / Brevo relay prod): 100% env-driven, no hardcoded hosts.
builder.Services.AddOptions<SmtpOptions>()
    .Bind(builder.Configuration.GetSection(SmtpOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddSingleton<IEmailSender, SmtpEmailSender>();

// -- Redis dedup (idempotency, 7-day TTL). Compose passes REDIS__HOST/REDIS__PORT;
// local `dotnet run` falls back to REDIS_HOST/REDIS_PORT, then localhost:6379.
var redisHost = builder.Configuration["REDIS:HOST"]
    ?? builder.Configuration["REDIS_HOST"]
    ?? "localhost";
var redisPort = GetPort(builder.Configuration["REDIS:PORT"] ?? builder.Configuration["REDIS_PORT"], 6379);
var redisOptions = ConfigurationOptions.Parse($"{redisHost}:{redisPort}");
// Survive a slow-starting Redis: connect in the background instead of crashing.
redisOptions.AbortOnConnectFail = false;
builder.Services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisOptions));
builder.Services.AddSingleton<IIdempotencyStore, RedisIdempotencyStore>();

// -- RabbitMQ with EXPLICIT topology (data-model §1.3): topic exchange,
// one durable queue, routing keys, retry 2s/5s/10s, DLX/DLQ. Nothing relies on
// MassTransit auto-declare defaults.
// NOTE: broker credentials are only passed to the client below — never logged.
var rabbitHost = builder.Configuration["RABBITMQ_HOST"] ?? "localhost";
var rabbitPort = GetPort(builder.Configuration["RABBITMQ_PORT"], 5672);
var rabbitUser = builder.Configuration["RABBITMQ_USER"] ?? "guest";
var rabbitPassword = builder.Configuration["RABBITMQ_PASSWORD"] ?? "guest";

builder.Services.AddMassTransit(bus =>
{
    bus.AddConsumer<UserRegisteredConsumer>();
    bus.AddConsumer<PasswordResetRequestedConsumer>();

    bus.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(new Uri($"rabbitmq://{rabbitHost}:{rabbitPort}/"), h =>
        {
            h.Username(rabbitUser);
            h.Password(rabbitPassword);
        });

        // Unknown fields are ignored so additive-optional contract changes
        // stay backward compatible (contracts/events/v1 policy).
        cfg.ConfigureJsonSerializerOptions(options =>
        {
            EventJsonOptions.Configure(options);
            return options;
        });

        cfg.Message<UserRegisteredEvent>(m => m.SetEntityName(WorkerTopology.EventsExchange));
        cfg.Message<PasswordResetRequestedEvent>(m => m.SetEntityName(WorkerTopology.EventsExchange));

        cfg.Publish<UserRegisteredEvent>(p => p.ExchangeType = "topic");
        cfg.Publish<PasswordResetRequestedEvent>(p => p.ExchangeType = "topic");

        cfg.Send<UserRegisteredEvent>(s => s.UseRoutingKeyFormatter(_ => WorkerTopology.UserRegisteredRoutingKey));
        cfg.Send<PasswordResetRequestedEvent>(s => s.UseRoutingKeyFormatter(_ => WorkerTopology.PasswordResetRoutingKey));

        cfg.ReceiveEndpoint(WorkerTopology.EmailQueue, endpoint =>
        {
            endpoint.PrefetchCount = WorkerTopology.PrefetchCount;
            // The bindings below ARE the topology — no auto-declare surprises.
            endpoint.ConfigureConsumeTopology = false;

            endpoint.Bind<UserRegisteredEvent>(b => b.RoutingKey = WorkerTopology.UserRegisteredRoutingKey);
            endpoint.Bind<PasswordResetRequestedEvent>(b => b.RoutingKey = WorkerTopology.PasswordResetRoutingKey);

            // Retry 3x (2s, 5s, 10s per spec Q3); then to worker.email.dlx/.dlq.
            endpoint.UseMessageRetry(r => r.Intervals(WorkerTopology.RetryIntervals));
            endpoint.BindDeadLetterQueue(WorkerTopology.DeadLetterQueue, WorkerTopology.DeadLetterExchange, _ => { });

            endpoint.ConfigureConsumer<UserRegisteredConsumer>(context);
            endpoint.ConfigureConsumer<PasswordResetRequestedConsumer>(context);
        });
    });
});

// -- Observability: the two standard lines (AGENTS.md §3).
builder.Services.AddObservability("Worker");

var app = builder.Build();
app.MapPrometheusScrapingEndpoint();
app.Run();

static int GetPort(string? raw, int fallback) =>
    int.TryParse(raw, out var port) && port is > 0 and <= 65535 ? port : fallback;
