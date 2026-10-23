using Identity.Application.Abstractions;
using Identity.Application.Common.Dtos;
using Identity.Application.Common.Errors;
using Identity.Application.Common.Events;
using Identity.Application.Common.Mappers;
using Identity.Domain.Entities;
using Identity.Domain.Repositories;
using Longevia.Shared.Auth;

namespace Identity.Application.Features.Users;

public sealed class UpdateProfileUseCase(
    ICurrentUser currentUser,
    IUserRepository userRepository,
    IEventPublisher eventPublisher,
    IUnitOfWork unitOfWork)
{
    public async Task<ProfileResponse> ExecuteAsync(UpdateProfileRequest request, CancellationToken ct = default)
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

        var now = DateTimeOffset.UtcNow;

        if (user.Profile is null)
        {
            var newProfile = UserProfile.Create(
                Guid.NewGuid(),
                user.Id,
                request.BirthDate,
                request.BiologicalSex,
                request.HeightCm,
                request.ActivityLevel,
                request.DietaryPreference,
                request.Allergies,
                now);

            user.SetProfile(newProfile);
            userRepository.AddProfile(newProfile);
        }
        else
        {
            user.Profile.Update(
                request.BirthDate,
                request.BiologicalSex,
                request.HeightCm,
                request.ActivityLevel,
                request.DietaryPreference,
                request.Allergies,
                now);
        }

        await eventPublisher.PublishAsync(new UserProfileUpdatedIntegrationEvent(user.Id, now), ct);
        await unitOfWork.SaveChangesAsync(ct);

        return user.Profile!.ToResponse();
    }
}
