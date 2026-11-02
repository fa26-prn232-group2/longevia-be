using Diet.Api;
using Diet.Api.Endpoints;
using Diet.Application;
using Diet.Infrastructure;
using Diet.Infrastructure.Messaging;
using Diet.Infrastructure.Persistence;
using Longevia.Shared.Auth;
using Longevia.Shared.Observability;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDietApplication();
builder.Services.AddDietInfrastructure(builder.Configuration);
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

public partial class Program;
