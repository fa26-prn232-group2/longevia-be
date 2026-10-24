using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
using Progress.Api.Auth;
using Progress.Api.Endpoints;
using Progress.Infrastructure.Persistence;

namespace Progress.Api.Extensions;

public static class HostExtensions
{
    public static IServiceCollection AddProgressHost(this IServiceCollection services, IConfiguration config)
    {
        services.AddProgressAuthentication(config);
        services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
            context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier);
        services.AddExceptionHandler<ProgressExceptionHandler>();
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo { Title = "Longevia Progress API", Version = "v1" });
            options.AddServer(new OpenApiServer { Url = "/api/progress" });
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
        return services;
    }

    public static WebApplication UseProgressHost(this WebApplication app)
    {
        app.UseExceptionHandler();
        app.UseStatusCodePages();
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI(options => options.SwaggerEndpoint("v1/swagger.json", "Progress v1"));
        }
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapGet("/alive", () => TypedResults.Ok(new { status = "ok" })).AllowAnonymous();
        app.MapGet("/health", async (ProgressDbContext db, CancellationToken ct) =>
            await db.Database.CanConnectAsync(ct) ? Results.Ok(new { status = "ok" }) : Results.StatusCode(503))
            .AllowAnonymous();
        app.MapFastingEndpoints();
        return app;
    }

    public static async Task InitializeProgressDatabaseAsync(this WebApplication app, CancellationToken ct)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ProgressDbContext>();
        await ProgressDatabaseInitializer.InitializeAsync(db, ct);
        app.Logger.LogInformation("Progress database initialization completed.");
    }
}
