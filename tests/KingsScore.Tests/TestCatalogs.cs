using Kings.Score.Catalog;
using Kings.Score.Contracts.Score;
using Kings.Score.Contracts.Snapshot;

namespace KingsScore.Tests;

/// <summary>Catalogue de TEST minimal (implémentation d'<see cref="ICatalog"/>).</summary>
public sealed class TestCatalog : ICatalog
{
    public IReadOnlyList<CatalogTweak> Tweaks { get; init; } = Array.Empty<CatalogTweak>();
    public IReadOnlyList<Incompatibility> Incompatibilities { get; init; } = Array.Empty<Incompatibility>();
}

/// <summary>
/// Réglages de test. Le catalogue « seed » n'utilise QUE des exemples sûrs et réversibles du socle §8.3
/// (plan d'alimentation, TRIM, résolution du timer) — aucun overclocking. Les réglages OC ci-dessous sont
/// clairement SYNTHÉTIQUES, isolés aux tests pour exercer la maximisation du gain ; jamais le vrai catalogue.
/// </summary>
public static class TestCatalogs
{
    public static ICatalog Seed => new TestCatalog
    {
        Tweaks = new[] { PowerPlanHighPerf, TrimEnable, TimerResolution },
    };

    // ----- Seeds sûrs (non-OC) -----

    public static readonly CatalogTweak PowerPlanHighPerf = new()
    {
        Id = "cpu.powerplan.highperf",
        Domain = SubscoresDomain.Cpu,
        RiskLevel = CatalogRiskLevel.VeryLow,
        Source = TweakSource.Internal,
        AppliesTo = s => !IsHighPerf(s.SettingsState.Cpu.PowerPlan),
        ApplyEffect = ss => ss.Cpu.PowerPlan = "High performance",
    };

    public static readonly CatalogTweak TrimEnable = new()
    {
        Id = "storage.trim.enable",
        Domain = SubscoresDomain.Storage,
        RiskLevel = CatalogRiskLevel.VeryLow,
        Source = TweakSource.Internal,
        AppliesTo = s => HasSsdOrNvme(s) && !s.SettingsState.Storage.TrimEnabled,
        ApplyEffect = ss => ss.Storage.TrimEnabled = true,
    };

    public static readonly CatalogTweak TimerResolution = new()
    {
        Id = "system.timer.resolution",
        Domain = SubscoresDomain.System,
        RiskLevel = CatalogRiskLevel.VeryLow,
        Source = TweakSource.Internal,
        AppliesTo = s => s.SettingsState.System.TimerResolutionMs > 1.0,
        ApplyEffect = ss => ss.System.TimerResolutionMs = 0.5,
    };

    // ----- OC synthétiques (tests uniquement) -----

    public static CatalogTweak Oc(string id, decimal gainPct, CatalogRiskLevel risk = CatalogRiskLevel.VeryLow) => new()
    {
        Id = id,
        Domain = SubscoresDomain.Gpu,
        RiskLevel = risk,
        Source = TweakSource.VendorSdk,
        IsOverclocking = true,
        AppliesTo = _ => true,
        ExpectedGainPct = gainPct,
        RevertExact = $"revert:{id}",
    };

    private static bool HasSsdOrNvme(SystemSnapshot s)
        => s.Hardware.Storage.Any(d => d.Type is StorageType.SSD or StorageType.NVMe);

    private static bool IsHighPerf(string? plan)
        => plan is not null && plan.Trim().Equals("High performance", StringComparison.OrdinalIgnoreCase);
}
