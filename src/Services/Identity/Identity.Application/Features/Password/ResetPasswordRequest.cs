namespace Identity.Application.Features.Password;

public sealed record ResetPasswordRequest(string Token, string NewPassword);
