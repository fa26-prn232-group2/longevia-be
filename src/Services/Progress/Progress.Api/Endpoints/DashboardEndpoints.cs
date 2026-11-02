using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Progress.Application.Features.Dashboard;

namespace Progress.Api.Endpoints;

public static class DashboardEndpoints
{
    public static void MapDashboardEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/dashboard").WithTags("Dashboard").RequireAuthorization();

        group.MapGet("/summary", async (DashboardUseCases useCases, CancellationToken ct) =>
        {
            var result = await useCases.GetSummaryAsync(ct);
            return TypedResults.Ok(result);
        })
        .WithName("GetDashboardSummary")
        .WithSummary("Tổng hợp số liệu dashboard, streak, và delta cân nặng");
    }
}
