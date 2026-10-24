namespace Identity.Application.Features.Password;

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
