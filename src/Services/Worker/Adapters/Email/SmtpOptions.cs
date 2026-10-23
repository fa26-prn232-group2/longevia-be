using System.ComponentModel.DataAnnotations;

namespace Longevia.Worker.Adapters.Email;

/// <summary>
/// Binds 1:1 to the <c>SMTP</c> configuration section (env <c>SMTP__*</c>).
/// Two profiles, selected 100% by environment — no code switches:
/// Mailpit (no auth, <c>UseSsl=false</c>) and Brevo relay
/// (credentials, <c>UseSsl=true</c> = StartTLS on port 587).
/// NEVER log these values: <see cref="Password"/> is a long-lived secret and
/// must never appear in logs, metrics labels, or exceptions.
/// </summary>
public sealed class SmtpOptions
{
    /// <summary>Configuration section name (<c>SMTP__HOST</c> maps to <c>SMTP:HOST</c>).</summary>
    public const string SectionName = "SMTP";

    /// <summary>SMTP relay host: mailpit/localhost (dev) or smtp-relay.brevo.com (prod).</summary>
    [Required]
    public string Host { get; set; } = "localhost";

    /// <summary>1025 for Mailpit, 587 for Brevo relay.</summary>
    [Range(1, 65535)]
    public int Port { get; set; } = 1025;

    /// <summary>
    /// false = plain connection (Mailpit); true = explicit TLS upgrade
    /// (StartTLS, Brevo relay on 587).
    /// </summary>
    public bool UseSsl { get; set; }

    /// <summary>Empty for Mailpit (no auth); Brevo login key in production.</summary>
    public string User { get; set; } = string.Empty;

    /// <summary>Empty for Mailpit; Brevo SMTP key in production. Never logged.</summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>Visible sender address.</summary>
    [Required]
    [EmailAddress]
    public string SenderEmail { get; set; } = "no-reply@longevia.local";
}
