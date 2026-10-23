namespace Longevia.Shared.Auth;

public interface ICurrentUser
{
    Guid? UserId { get; }
    string? Email { get; }
    bool IsAuthenticated { get; }
    bool InRole(string role);
}
