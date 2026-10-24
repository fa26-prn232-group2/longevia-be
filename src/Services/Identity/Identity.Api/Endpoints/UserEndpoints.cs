using Identity.Api.Extensions;
using Identity.Application.Features.Users;

namespace Identity.Api.Endpoints;

public static class UserEndpoints
{
    public static IEndpointRouteBuilder MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/users")
            .WithTags("Users")
            .RequireAuthorization();

        group.MapGet("/me", async (
            GetCurrentUserUseCase useCase,
            CancellationToken ct) =>
        {
            var result = await useCase.ExecuteAsync(ct);
            return TypedResults.Ok(result);
        })
        .WithName("GetCurrentUser")
        .WithSummary("Lấy thông tin tài khoản hiện tại kèm hồ sơ");

        group.MapPut("/me/profile", async (
            UpdateProfileRequest request,
            UpdateProfileUseCase useCase,
            CancellationToken ct) =>
        {
            var result = await useCase.ExecuteAsync(request, ct);
            return TypedResults.Ok(result);
        })
        .AddIdentityValidation<UpdateProfileRequest>()
        .WithName("UpdateProfile")
        .WithSummary("Cập nhật hồ sơ thể chất cơ bản & dị ứng");

        return app;
    }
}
