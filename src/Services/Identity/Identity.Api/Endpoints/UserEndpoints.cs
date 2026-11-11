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

        group.MapPatch("/me", async (
            ChangeFullNameRequest request,
            ChangeFullNameUseCase useCase,
            CancellationToken ct) =>
        {
            var result = await useCase.ExecuteAsync(request, ct);
            return TypedResults.Ok(result);
        })
        .AddIdentityValidation<ChangeFullNameRequest>()
        .WithName("ChangeFullName")
        .WithSummary("Đổi họ tên hiển thị của tài khoản hiện tại");

        group.MapPost("/me/email-change", async (
            RequestEmailChangeRequest request,
            RequestEmailChangeUseCase useCase,
            CancellationToken ct) =>
        {
            await useCase.ExecuteAsync(request, ct);
            return TypedResults.Accepted((string?)null);
        })
        .AddIdentityValidation<RequestEmailChangeRequest>()
        .WithName("RequestEmailChange")
        .WithSummary("Gửi link xác nhận đổi email tới địa chỉ mới");

        group.MapGet("/me/preferences", async (PreferenceUseCases useCases, CancellationToken ct) =>
            TypedResults.Ok(await useCases.GetAsync(ct)))
            .WithName("GetPreferences").WithSummary("Lấy tùy chọn nhận báo cáo tuần");

        group.MapPut("/me/preferences", async (UpdatePreferencesRequest request,
            PreferenceUseCases useCases, CancellationToken ct) =>
            TypedResults.Ok(await useCases.UpdateAsync(request, ct)))
            .WithName("UpdatePreferences").WithSummary("Bật/tắt nhận báo cáo tuần");

        return app;
    }
}
