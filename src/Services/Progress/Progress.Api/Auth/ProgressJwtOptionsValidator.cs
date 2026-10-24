using System.Security.Cryptography;
using System.Text;
using Longevia.Shared.Auth;
using Microsoft.Extensions.Options;

namespace Progress.Api.Auth;

public sealed class ProgressJwtOptionsValidator : IValidateOptions<JwtOptions>
{
    public ValidateOptionsResult Validate(string? name, JwtOptions options)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(options.Issuer) || string.IsNullOrWhiteSpace(options.Audience)
            || string.IsNullOrWhiteSpace(options.KeyId))
        {
            errors.Add("Jwt:Issuer, Audience và KeyId là bắt buộc.");
        }
        if (!string.IsNullOrWhiteSpace(options.SigningPrivateKey))
        {
            errors.Add("Progress chỉ nhận public key; không cấu hình Jwt:SigningPrivateKey.");
        }
        if (!IsPublicP256(options.SigningPublicKey))
        {
            errors.Add("Jwt:SigningPublicKey phải là base64 của SPKI PEM ECDSA P-256.");
        }
        var hasPreviousKey = !string.IsNullOrWhiteSpace(options.PreviousPublicKey);
        var hasPreviousId = !string.IsNullOrWhiteSpace(options.PreviousKeyId);
        if (hasPreviousKey != hasPreviousId || (hasPreviousKey &&
            (!IsPublicP256(options.PreviousPublicKey) || options.PreviousKeyId == options.KeyId)))
        {
            errors.Add("PreviousPublicKey/PreviousKeyId phải đi cặp, public P-256, với kid khác key hiện tại.");
        }
        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }

    private static bool IsPublicP256(string? encoded)
    {
        if (string.IsNullOrWhiteSpace(encoded))
        {
            return false;
        }
        try
        {
            var pem = Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
            var fields = PemEncoding.Find(pem);
            if (pem[fields.Label] != "PUBLIC KEY" || !string.IsNullOrWhiteSpace(pem[..fields.Location.Start])
                || !string.IsNullOrWhiteSpace(pem[fields.Location.End..]))
            {
                return false;
            }
            using var key = ECDsa.Create();
            key.ImportFromPem(pem);
            return key.ExportParameters(false).Curve.Oid.Value == ECCurve.NamedCurves.nistP256.Oid.Value;
        }
        catch (Exception exception) when (exception is ArgumentException or FormatException or CryptographicException)
        {
            return false;
        }
    }
}
