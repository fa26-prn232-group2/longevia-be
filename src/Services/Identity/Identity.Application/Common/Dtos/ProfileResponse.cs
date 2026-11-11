namespace Identity.Application.Common.Dtos;

public sealed record ProfileResponse(
    Guid Id,
    DateOnly BirthDate,
    string BiologicalSex,
    decimal HeightCm,
    string ActivityLevel,
    string DietaryPreference,
    IReadOnlyList<string> Allergies,
    IReadOnlyList<string> HealthLimitations,
    DateTimeOffset UpdatedAt,
    string Goal = "Maintain");
