using Identity.Application.Abstractions;
using Identity.Application.Common.Errors;
using Identity.Domain.Repositories;
using Longevia.Shared.Auth;

namespace Identity.Application.Features.Users;

public sealed record PreferencesResponse(bool ReceiveWeeklyDigest);
public sealed record UpdatePreferencesRequest(bool ReceiveWeeklyDigest);

public sealed class PreferenceUseCases(ICurrentUser currentUser, IUserRepository users, IUnitOfWork unitOfWork)
{
    public async Task<PreferencesResponse> GetAsync(CancellationToken ct)
    {
        var user = await RequireUserAsync(ct);
        return new(user.Profile?.ReceiveWeeklyDigest ?? false);
    }

    public async Task<PreferencesResponse> UpdateAsync(UpdatePreferencesRequest request, CancellationToken ct)
    {
        var user = await RequireUserAsync(ct);
        if (user.Profile is null) throw UserErrors.NotFound(user.Id);
        user.Profile.SetWeeklyDigest(request.ReceiveWeeklyDigest, DateTimeOffset.UtcNow);
        await unitOfWork.SaveChangesAsync(ct);
        return new(user.Profile.ReceiveWeeklyDigest);
    }

    private async Task<Identity.Domain.Entities.User> RequireUserAsync(CancellationToken ct)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is null) throw AuthErrors.Unauthorized();
        return await users.GetByIdWithProfileAsync(currentUser.UserId.Value, ct)
            ?? throw UserErrors.NotFound(currentUser.UserId.Value);
    }
}
