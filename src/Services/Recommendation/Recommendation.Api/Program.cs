using Recommendation.Api;

var builder = WebApplication.CreateBuilder(args);

RecommendationStartup.Configure(builder);

var app = builder.Build();

await RecommendationStartup.RunAsync(app);

app.Run();
