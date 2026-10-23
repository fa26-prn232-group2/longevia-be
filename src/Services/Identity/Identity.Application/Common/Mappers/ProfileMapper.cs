using Identity.Application.Common.Dtos;
using Identity.Domain.Entities;

namespace Identity.Application.Common.Mappers;

public static class ProfileMapper
{
    public static ProfileResponse ToResponse(this UserProfile profile) =>
        new(
            profile.Id,
            profile.BirthDate,
            profile.BiologicalSex,
            profile.HeightCm,
            profile.ActivityLevel,
            profile.DietaryPreference,
            profile.Allergies,
            profile.UpdatedAt);
}
