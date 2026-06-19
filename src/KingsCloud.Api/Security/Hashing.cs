using System.Security.Cryptography;
using System.Text;

namespace Kings.Cloud.Api.Security;

/// <summary>
/// Hachage et génération de jetons. Confidentialité by design (C8) : clés de licence, jetons de
/// session et identités de compte sont stockés HACHÉS (jamais en clair). Anti-piratage.
/// </summary>
public static class Hashing
{
    public static string Sha256Hex(string input)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input))).ToLowerInvariant();

    /// <summary>Jeton opaque aléatoire (256 bits). Le hachage seul est stocké côté serveur.</summary>
    public static string NewToken()
        => Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
}
