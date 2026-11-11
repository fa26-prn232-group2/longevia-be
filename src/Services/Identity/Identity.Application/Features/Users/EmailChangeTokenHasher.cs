using System.Security.Cryptography;
using System.Text;

namespace Identity.Application.Features.Users;

internal static class EmailChangeTokenHasher
{
    public static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
