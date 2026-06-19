using System.Security.Cryptography;
using System.Text;

namespace Kings.Cloud.Api.Security;

/// <summary>
/// Hache les identités/clés à FAIBLE entropie (identité de compte, clé de licence) avec un
/// HMAC-SHA256 à <b>pepper</b> serveur (clé secrète hors dépôt). Un SHA-256 nu sur ces valeurs serait
/// réversible par rainbow tables / énumération — ce qui ne tiendrait pas la promesse C8 (« pas d'e-mail
/// en clair »). Le HMAC garde la recherche déterministe par index unique tout en bloquant ces attaques.
/// (Les jetons de session, eux, sont des aléas 256 bits : un SHA-256 nu y suffit, voir <see cref="Hashing"/>.)
/// En prod, le pepper est injecté par config/env (jamais committé), comme PACK_SIGNING_KEY.
/// </summary>
public sealed class IdentityHasher
{
    private readonly byte[] _pepper;

    public IdentityHasher(IConfiguration config)
    {
        var pepper = config["Security:HashPepper"]
                     ?? Environment.GetEnvironmentVariable("KINGS_HASH_PEPPER")
                     ?? "DEV-HASH-PEPPER"; // DEV LOCAL uniquement
        _pepper = Encoding.UTF8.GetBytes(pepper);
    }

    public string Hash(string input)
        => Convert.ToHexString(HMACSHA256.HashData(_pepper, Encoding.UTF8.GetBytes(input))).ToLowerInvariant();
}
