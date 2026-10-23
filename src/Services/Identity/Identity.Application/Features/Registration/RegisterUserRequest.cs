namespace Identity.Application.Features.Registration;

public sealed record RegisterUserRequest(
    string Email,
    string Password,
    string FullName);
