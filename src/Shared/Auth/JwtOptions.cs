using System.ComponentModel.DataAnnotations;

namespace Longevia.Shared.Auth;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    [Required]
    public string Issuer { get; set; } = "longevia";

    [Required]
    public string Audience { get; set; } = "longevia-clients";

    [Required]
    public string KeyId { get; set; } = "longevia-dev-01";

    public string? SigningPublicKey { get; set; }

    public string? SigningPrivateKey { get; set; }

    public string? PreviousKeyId { get; set; }

    public string? PreviousPublicKey { get; set; }

    public int AccessTokenExpiryMinutes { get; set; } = 15;

    public int RefreshTokenExpiryDays { get; set; } = 7;
}
