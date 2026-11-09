using Diet.Api;
using Diet.Api.Endpoints;
using Diet.Api.Grpc;
using Diet.Application;
using Diet.Application.Common;
using Diet.Infrastructure;
using Diet.Infrastructure.Messaging;
using Diet.Infrastructure.Persistence;
using Longevia.Shared.Auth;
using Longevia.Shared.Observability;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

var httpPort = ResolvePort(builder.Configuration["DIET:HTTP_PORT"], 8080);
var grpcPort = ResolvePort(builder.Configuration["DIET:GRPC_PORT"], 50051);
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(httpPort, listen => listen.Protocols = HttpProtocols.Http1);
    options.ListenAnyIP(grpcPort, listen => listen.Protocols = HttpProtocols.Http2);
});

builder.Services.AddDietApplication();
builder.Services.AddDietInfrastructure(builder.Configuration);
builder.Services.AddGrpc(options =>
{
    var serviceToken = builder.Configuration["SERVICE:TOKEN"];
    if (!string.IsNullOrWhiteSpace(serviceToken))
    {
        builder.Services.AddSingleton(new ServiceTokenServerInterceptor(serviceToken));
        options.Interceptors.Add<ServiceTokenServerInterceptor>();
    }
});
builder.Services.AddOptions<RecommendationBudgetOptions>()
    .Bind(builder.Configuration.GetSection(RecommendationBudgetOptions.SectionName))
    .Validate(options => RecommendationBudgetOptions.IsValidDeadline(options.DeadlineMilliseconds),
        "Recommendation:DeadlineMilliseconds phải nằm trong khoảng 1.000..60.000 ms.")
    .ValidateOnStart();
builder.Services.AddOptions<JwtOptions>().Bind(builder.Configuration.GetSection(JwtOptions.SectionName))
    .Validate(options => !string.IsNullOrWhiteSpace(options.SigningPublicKey),
        "Diet yêu cầu Jwt:SigningPublicKey.").ValidateOnStart();
builder.Services.AddJwtAuthentication(builder.Configuration);
builder.Services.AddAuthorization();
builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
    context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier);
builder.Services.AddExceptionHandler<DietExceptionHandler>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "Longevia Diet API", Version = "v1" });
    options.AddServer(new OpenApiServer { Url = "/api/diet" });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
    });
    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        { new OpenApiSecuritySchemeReference("Bearer", document), new List<string>() },
    });
});
builder.Services.AddObservability("DietService");

var app = builder.Build();
app.UseExceptionHandler();
app.UseStatusCodePages();
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.UseAuthentication();
app.UseAuthorization();
app.MapFoodEndpoints();
app.MapTargetEndpoints();
app.MapMealEndpoints();
app.MapMealPlanEndpoints();
app.MapNutritionEndpoints();
app.MapKnowledgeEndpoints();
app.MapGrpcService<DietGrpcService>();
app.MapGet("/health", async (DietDbContext db, CancellationToken ct) =>
    await db.Database.CanConnectAsync(ct) ? Results.Ok(new { status = "ok" }) : Results.StatusCode(503));
app.MapGet("/alive", () => TypedResults.Ok(new { status = "ok" }));
app.MapPrometheusScrapingEndpoint();

await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<DietDbContext>();
    await db.Database.MigrateAsync();
    if (app.Configuration["export-meal-activities"] is { Length: > 0 } snapshotPath)
    {
        var count = await scope.ServiceProvider.GetRequiredService<MealActivitySnapshotExporter>().ExportAsync(snapshotPath);
        app.Logger.LogInformation("Meal activity snapshot exported: {Count} records. HTTP and bus were not started.", count);
        return;
    }
}

app.Run();

static int ResolvePort(string? raw, int fallback) =>
    int.TryParse(raw, out var port) && port is > 0 and <= 65535 ? port : fallback;

public partial class Program;
