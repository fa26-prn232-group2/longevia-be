using Identity.Application.Abstractions;
using Identity.Application.Common.Dtos;
using Identity.Application.Common.Errors;
using Identity.Application.Common.Mappers;
using Identity.Domain.Repositories;
using Longevia.Shared.Auth;

namespace Identity.Application.Features.Users;

/// <summary>Đổi tên hiển thị của tài khoản hiện tại.</summary>
public sealed class ChangeFullNameUseCase(
    ICurrentUser currentUser,
    IUserRepository userRepository,
    IUnitOfWork unitOfWork)
{
    public async Task<UserResponse> ExecuteAsync(ChangeFullNameRequest request,
        CancellationToken ct = default)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is null)
        {
            throw AuthErrors.Unauthorized();
        }

        var user = await userRepository.GetByIdWithProfileAsync(currentUser.UserId.Value, ct);
        if (user is null)
        {
            throw UserErrors.NotFound(currentUser.UserId.Value);
        }

        user.ChangeFullName(request.FullName, DateTimeOffset.UtcNow);
        userRepository.Update(user);
        await unitOfWork.SaveChangesAsync(ct);
        return user.ToResponse();
    }
}
