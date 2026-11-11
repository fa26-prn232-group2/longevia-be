namespace Identity.Application.Features.Users;

public sealed record RequestEmailChangeRequest(string NewEmail, string CurrentPassword);
