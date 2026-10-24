using Longevia.Shared.Auth;
using Longevia.Shared.Errors;
using Longevia.Shared.Validation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace Progress.IntegrationTests;

// Only the test host maps probes; no diagnostic/business routes ship in Progress.Api.
public sealed class FoundationProbeStartupFilter : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        next(app);
        app.UseEndpoints(endpoints =>
        {
            endpoints.MapGet("/_test/subject", (ICurrentUser user, TimeProvider clock) =>
                TypedResults.Ok(new { user.UserId, user.IsAuthenticated, now = clock.GetUtcNow() }))
                .RequireAuthorization();
            endpoints.MapPost("/_test/validate", (ProbeRequest request) => TypedResults.Ok(request))
                .AddEndpointFilter<ValidationFilter<ProbeRequest>>().RequireAuthorization();
            endpoints.MapGet("/_test/fault", (HttpContext _) => ThrowUnexpected()).RequireAuthorization();
            endpoints.MapGet("/_test/conflict", (HttpContext _) => ThrowConflict()).RequireAuthorization();
        });
    };

    private static IResult ThrowUnexpected() => throw new InvalidOperationException("test-sensitive-value");
    private static IResult ThrowConflict() => throw AppException.Conflict("Xung đột kiểm thử.", "progress.test_conflict");
}
