using Longevia.Shared.Auth;
using Longevia.Shared.Errors;

namespace Progress.Application.Common;

internal static class CurrentUserExtensions
{
    internal static Guid RequiredId(this ICurrentUser user) =>
        user.IsAuthenticated && user.UserId is { } id && id != Guid.Empty
            ? id : throw AppException.Unauthorized("Bạn cần đăng nhập.", "progress.unauthorized");
}
