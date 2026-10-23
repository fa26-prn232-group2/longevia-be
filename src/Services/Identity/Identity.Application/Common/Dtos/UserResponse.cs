namespace Identity.Application.Common.Dtos;

public sealed record UserResponse(
    Guid Id,
    string Email,
    string FullName,
    string Status,
    DateTimeOffset CreatedAt,
    ProfileResponse? Profile = null);
