using System.Security.Cryptography;
using System.Text;

namespace Kings.Cloud.Api.Packs;

/// <summary>
/// Signe le manifeste d'un pack. ⚠️ Mécanisme de DÉV (HMAC-SHA256 avec une clé de config). En PROD,
/// la signature est posée avec une **clé asymétrique gérée par l'humain** (jamais dans le dépôt —
/// garde-fou #8), et la **vérification est faite côté Cœur** (Lot A) avec la clé publique. Ici on
/// fournit le mécanisme + une clé de dév ; on ne fige pas la cryptographie de prod.
/// </summary>
public sealed class PackSigner
{
    private readonly byte[] _key;

    public PackSigner(IConfiguration config)
    {
        var secret = config["Packs:SigningKey"]
                     ?? Environment.GetEnvironmentVariable("PACK_SIGNING_KEY")
                     ?? "DEV-PACK-SIGNING-KEY"; // DEV LOCAL uniquement
        _key = Encoding.UTF8.GetBytes(secret);
    }

    public string Sign(string canonical)
        => Convert.ToBase64String(HMACSHA256.HashData(_key, Encoding.UTF8.GetBytes(canonical)));

    /// <summary>Chaîne canonique déterministe du manifeste (ce qui est signé).</summary>
    public static string Canonical(Guid packId, string version, string minAppVersion, string? weightsetVersion, string payloadJson)
        => string.Join("\n", packId, version, minAppVersion, weightsetVersion ?? "", payloadJson);
}
