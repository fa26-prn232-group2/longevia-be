using Identity.Application.Common.Dtos;
using Identity.Application.Common.Errors;
using Identity.Application.Common.Mappers;
using Identity.Domain.Repositories;
using Longevia.Shared.Auth;

namespace Identity.Application.Features.Users;

public sealed class GetCurrentUserUseCase(
    ICurrentUser currentUser,
    IUserRepository userRepository)
{
    public async Task<UserResponse> ExecuteAsync(CancellationToken ct = default)
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

        return user.ToResponse();
    }
}
