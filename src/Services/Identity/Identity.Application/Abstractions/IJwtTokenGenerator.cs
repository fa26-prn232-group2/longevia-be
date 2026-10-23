using Identity.Domain.Entities;

namespace Identity.Application.Abstractions;

public interface IJwtTokenGenerator
{
    string GenerateAccessToken(User user, IReadOnlyList<string>? roles = null);
    RefreshToken GenerateRefreshToken(Guid userId);
}
