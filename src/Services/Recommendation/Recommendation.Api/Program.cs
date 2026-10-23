using Recommendation.Api;
using Recommendation.Application.Abstractions;

// `--bootstrap [--seed /seed] [--skip-if-loaded] [--verify]` dùng cho bước deploy
// `kb-bootstrap`: chạy EF migration rồi nạp seed đã xuất, không mở Kestrel/gRPC và
// không cần GOOGLE_AI__API_KEY.
// `--migrate-only` là chế độ cũ (kb-schema), chỉ chạy migration.
var bootstrap = args.Contains("--bootstrap", StringComparer.Ordinal);
var migrateOnly = !bootstrap && args.Contains("--migrate-only", StringComparer.Ordinal);

SeedLoadOptions? bootstrapOptions = null;
if (bootstrap)
{
    try
    {
        bootstrapOptions = ParseSeedOptions(args);
    }
    catch (ArgumentException exception)
    {
        Console.Error.WriteLine(exception.Message);
        return 1;
    }
}

var builder = WebApplication.CreateBuilder(args);

RecommendationStartup.Configure(builder, migrateOnly, bootstrap);

var app = builder.Build();

if (bootstrapOptions is not null)
{
    return await RecommendationStartup.BootstrapAsync(app, bootstrapOptions);
}

if (migrateOnly)
{
    await RecommendationStartup.MigrateOnlyAsync(app);
}
else
{
    await RecommendationStartup.RunAsync(app);
    app.Run();
}

return 0;

static SeedLoadOptions ParseSeedOptions(string[] arguments)
{
    var seedPath = "/seed";

    for (var index = 0; index < arguments.Length; index++)
    {
        var argument = arguments[index];
        if (argument.StartsWith("--seed=", StringComparison.Ordinal))
        {
            seedPath = argument["--seed=".Length..];
        }
        else if (string.Equals(argument, "--seed", StringComparison.Ordinal))
        {
            if (index + 1 >= arguments.Length
                || arguments[index + 1].StartsWith("--", StringComparison.Ordinal))
            {
                throw new ArgumentException("--seed yêu cầu một đường dẫn.");
            }

            seedPath = arguments[index + 1];
            index++;
        }
    }

    if (string.IsNullOrWhiteSpace(seedPath))
    {
        throw new ArgumentException("--seed yêu cầu một đường dẫn.");
    }

    return new SeedLoadOptions
    {
        SeedPath = seedPath,
        SkipIfLoaded = arguments.Contains("--skip-if-loaded", StringComparer.Ordinal),
        Verify = arguments.Contains("--verify", StringComparer.Ordinal),
    };
}
