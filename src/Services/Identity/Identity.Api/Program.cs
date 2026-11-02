using Identity.Api.Endpoints;
using Identity.Api.Middlewares;
using Identity.Application;
using Identity.Infrastructure;
using Identity.Infrastructure.Persistence;
using Longevia.Shared.Auth;
using Longevia.Shared.Observability;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);
var httpPort = builder.Configuration.GetValue("IDENTITY:HTTP_PORT", 8080);
var grpcPort = builder.Configuration.GetValue("IDENTITY:GRPC_PORT", 50051);
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(httpPort, listen => listen.Protocols = HttpProtocols.Http1);
    options.ListenAnyIP(grpcPort, listen => listen.Protocols = HttpProtocols.Http2);
});

builder.Services.AddIdentityApplication();
builder.Services.AddIdentityInfrastructure(builder.Configuration);
builder.Services.AddGrpc();
builder.Services.AddJwtAuthentication(builder.Configuration);
builder.Services.AddAuthorization();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Longevia Identity API",
        Version = "v1",
        Description = "Identity Service API - Authentication, Profiles, Tokens",
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Nhập Access Token JWT (không cần tiền tố 'Bearer ').",
    });

    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        { new OpenApiSecuritySchemeReference("Bearer", document), new List<string>() },
    });
});

builder.Services.AddObservability("IdentityService");

var app = builder.Build();

app.UseMiddleware<GlobalExceptionHandlerMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapAuthEndpoints();
app.MapUserEndpoints();
app.MapGrpcService<IdentityGrpcService>();
app.MapPrometheusScrapingEndpoint();

if (app.Configuration.GetValue("Database:AutoMigrate", true))
{
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
    await db.Database.MigrateAsync();
}

app.Run();

public partial class Program;
