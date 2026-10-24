namespace Identity.Application.Features.Users;

public sealed record UpdateProfileRequest(
    DateOnly BirthDate,
    string BiologicalSex,
    decimal HeightCm,
    string ActivityLevel,
    string DietaryPreference,
    List<string>? Allergies);
