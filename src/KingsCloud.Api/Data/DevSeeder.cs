using Kings.Cloud.Api.Packs;
using Kings.Cloud.Api.Security;
using Microsoft.EntityFrameworkCore;

namespace Kings.Cloud.Api.Data;

/// <summary>
/// Données de DÉVELOPPEMENT (idempotent) : un compte + une licence Pro, et un pack d'exemple signé.
/// En prod, comptes/licences sont provisionnés hors-bande (paiement/licence) et les packs sont publiés
/// et signés côté humain ; pas de seed.
/// </summary>
public static class DevSeeder
{
    public const string DevLicenseKey = "DEV-LICENSE-KEY";

    public static async Task SeedAsync(KingsCloudDbContext db, PackSigner signer, IdentityHasher identityHasher)
    {
        await SeedAccountAsync(db, identityHasher);
        await SeedPackAsync(db, signer);
    }

    private static async Task SeedAccountAsync(KingsCloudDbContext db, IdentityHasher identityHasher)
    {
        if (await db.Accounts.AnyAsync()) return;

        var account = new Account
        {
            Id = Guid.NewGuid(),
            AccountHash = identityHasher.Hash("dev-user"),
            Display = "DevPlayer",
            Locale = "fr",
            CreatedAt = DateTimeOffset.UtcNow,
        };
        db.Accounts.Add(account);
        db.Licenses.Add(new License
        {
            Id = Guid.NewGuid(),
            AccountId = account.Id,
            LicenseKeyHash = identityHasher.Hash(DevLicenseKey),
            Plan = LicensePlan.Pro,
            Status = LicenseState.Active,
            ExpiresAt = DateTimeOffset.UtcNow.AddYears(1),
            OfflineToleranceDays = 14,
        });
        await db.SaveChangesAsync();
    }

    private static async Task SeedPackAsync(KingsCloudDbContext db, PackSigner signer)
    {
        if (await db.Packs.AnyAsync()) return;

        var packId = Guid.NewGuid();
        const string version = "1.0.0";
        const string minAppVersion = "1.0.0";
        const string weightsetVersion = "1.0.0";
        // Payload d'EXEMPLE (le vrai contenu du catalogue est un livrable humain, C5).
        const string payloadJson =
            """{"tweaks":[{"id":"system.timer.resolution","domain":"system","riskLevel":"veryLow"}],"services":[],"mappings":[]}""";

        db.Packs.Add(new Pack
        {
            Id = packId,
            Version = version,
            MinAppVersion = minAppVersion,
            WeightsetVersion = weightsetVersion,
            Signature = signer.Sign(PackSigner.Canonical(packId, version, minAppVersion, weightsetVersion, payloadJson)),
            PayloadJson = payloadJson,
            PublishedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();
    }
}
