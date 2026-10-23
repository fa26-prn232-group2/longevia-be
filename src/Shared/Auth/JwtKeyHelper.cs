using System.Security.Cryptography;
using System.Text;

namespace Longevia.Shared.Auth;

public static class JwtKeyHelper
{
    public static ECDsa ImportPublicKey(string base64Pem)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(base64Pem);

        try
        {
            var rawBytes = Convert.FromBase64String(base64Pem.Trim());
            var pem = Encoding.UTF8.GetString(rawBytes);

            var ecdsa = ECDsa.Create();
            ecdsa.ImportFromPem(pem);
            return ecdsa;
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException(
                "Định dạng Public Key không hợp lệ. Khóa phải là chuỗi Base64 của SPKI PEM trên một dòng.",
                ex);
        }
        catch (CryptographicException ex)
        {
            throw new InvalidOperationException(
                "Không thể nạp Public Key từ PEM. Hãy đảm bảo khóa là ECDsa (P-256) SPKI PEM hợp lệ.",
                ex);
        }
    }

    public static ECDsa ImportPrivateKey(string base64Pem)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(base64Pem);

        try
        {
            var rawBytes = Convert.FromBase64String(base64Pem.Trim());
            var pem = Encoding.UTF8.GetString(rawBytes);

            var ecdsa = ECDsa.Create();
            ecdsa.ImportFromPem(pem);
            return ecdsa;
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException(
                "Định dạng Private Key không hợp lệ. Khóa phải là chuỗi Base64 của PKCS#8 PEM trên một dòng.",
                ex);
        }
        catch (CryptographicException ex)
        {
            throw new InvalidOperationException(
                "Không thể nạp Private Key từ PEM. Hãy đảm bảo khóa là ECDsa (P-256) PKCS#8 PEM hợp lệ.",
                ex);
        }
    }
}
