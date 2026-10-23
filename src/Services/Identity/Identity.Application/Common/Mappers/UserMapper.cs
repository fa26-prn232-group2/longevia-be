using Identity.Application.Common.Dtos;
using Identity.Domain.Entities;

namespace Identity.Application.Common.Mappers;

public static class UserMapper
{
    public static UserResponse ToResponse(this User user) =>
        new(
            user.Id,
            user.Email,
            user.FullName,
            user.Status,
            user.CreatedAt,
            user.Profile?.ToResponse());
}
