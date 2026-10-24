namespace Identity.Application.Common.Dtos;

public sealed record ProfileResponse(
    Guid Id,
    DateOnly BirthDate,
    string BiologicalSex,
    decimal HeightCm,
    string ActivityLevel,
    string DietaryPreference,
    IReadOnlyList<string> Allergies,
    DateTimeOffset UpdatedAt);
