namespace Identity.Application.Features.Users;

public sealed record UpdateProfileRequest(
    DateOnly BirthDate,
    string BiologicalSex,
    int HeightCm,
    string ActivityLevel,
    string DietaryPreference,
    List<string>? Allergies);
