namespace Kings.Cloud.Api.Data;

/// <summary>Plan de licence (contrat OpenAPI : Free | Pro).</summary>
public enum LicensePlan { Free, Pro }

/// <summary>État de licence.</summary>
public enum LicenseState { Active, Expired, Invalid }

/// <summary>
/// Compte utilisateur. Confidentialité by design (C8/D7) : AUCUN e-mail en clair en base.
/// L'identité externe est stockée « hachable » (<see cref="AccountHash"/>) ; <see cref="Display"/>
/// est le seul identifiant public (pseudo du leaderboard).
/// </summary>
public sealed class Account
{
    public Guid Id { get; set; }
    public required string AccountHash { get; set; }   // hash de l'identité externe (jamais l'e-mail en clair)
    public required string Display { get; set; }        // pseudo public
    public string Locale { get; set; } = "en";
    public DateTimeOffset CreatedAt { get; set; }

    public License? License { get; set; }
}

/// <summary>Licence liée à un compte. La clé de licence est stockée HACHÉE (anti-piratage).</summary>
public sealed class License
{
    public Guid Id { get; set; }
    public Guid AccountId { get; set; }
    public required string LicenseKeyHash { get; set; }
    public LicensePlan Plan { get; set; }
    public LicenseState Status { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
    public int OfflineToleranceDays { get; set; }
    public string? PaymentRef { get; set; }

    public Account? Account { get; set; }
}

/// <summary>Session liée à la licence (jeton détenu par le Cœur). Le jeton est stocké HACHÉ.</summary>
public sealed class Session
{
    public Guid Id { get; set; }
    public required string TokenHash { get; set; }
    public Guid AccountId { get; set; }
    public Guid LicenseId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
}

/// <summary>
/// Entrée de classement. Le score est TOUJOURS recalculé côté serveur (anti-triche C3) ; jamais
/// la valeur cliente. Une entrée par compte (mise à jour).
/// </summary>
public sealed class LeaderboardEntry
{
    public Guid Id { get; set; }
    public Guid AccountId { get; set; }
    public int RecomputedScore { get; set; }       // 0..100, recalculé serveur
    public required string WeightsetVersion { get; set; }
    public required string Tier { get; set; }       // budget | mid | highEnd
    public required string ConfigHash { get; set; } // empreinte anonyme de la config
    public Guid SnapshotId { get; set; }            // traçabilité
    public DateTimeOffset UpdatedAt { get; set; }

    public Account? Account { get; set; }
}

/// <summary>Pack d'optimisation signé (manifeste + payload). La signature est posée côté humain.</summary>
public sealed class Pack
{
    public Guid Id { get; set; }
    public required string Version { get; set; }        // ^\d+\.\d+\.\d+$
    public required string MinAppVersion { get; set; }  // ^\d+\.\d+\.\d+$
    public string? WeightsetVersion { get; set; }
    public required string Signature { get; set; }
    public required string PayloadJson { get; set; }    // jsonb : tweaks/services/mappings (descripteur catalogue)
    public DateTimeOffset PublishedAt { get; set; }
}

/// <summary>
/// Résultat de télémétrie anonymisé (opt-in). Table prévue (CDC §13.2) ; AUCUN endpoint dans
/// l'OpenAPI v1.0.1 — on ne crée pas de route hors contrat (C6).
/// </summary>
public sealed class TelemetryResult
{
    public Guid Id { get; set; }
    public required string ConfigHash { get; set; }
    public required string TweakId { get; set; }
    public int ScoreDelta { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
