using System.Security.Cryptography;
using System.Text;

namespace Identity.Application.Features.Verification;

internal static class VerificationTokenHasher
{
    public static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
