using Kings.Cloud.Api.Security;
using Microsoft.EntityFrameworkCore;

namespace Kings.Cloud.Api.Data;

/// <summary>
/// Données de DÉVELOPPEMENT (idempotent) : un compte + une licence Pro pour pouvoir ouvrir une session.
/// En prod, comptes et licences sont provisionnés hors-bande (paiement/licence) ; pas de seed.
/// </summary>
public static class DevSeeder
{
    public const string DevLicenseKey = "DEV-LICENSE-KEY";

    public static async Task SeedAsync(KingsCloudDbContext db)
    {
        if (await db.Accounts.AnyAsync()) return;

        var account = new Account
        {
            Id = Guid.NewGuid(),
            AccountHash = Hashing.Sha256Hex("dev-user"),
            Display = "DevPlayer",
            Locale = "fr",
            CreatedAt = DateTimeOffset.UtcNow,
        };
        db.Accounts.Add(account);
        db.Licenses.Add(new License
        {
            Id = Guid.NewGuid(),
            AccountId = account.Id,
            LicenseKeyHash = Hashing.Sha256Hex(DevLicenseKey),
            Plan = LicensePlan.Pro,
            Status = LicenseState.Active,
            ExpiresAt = DateTimeOffset.UtcNow.AddYears(1),
            OfflineToleranceDays = 14,
        });
        await db.SaveChangesAsync();
    }
}
