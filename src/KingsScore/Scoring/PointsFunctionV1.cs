using Kings.Score.Contracts.Score;
using Kings.Score.Contracts.Snapshot;

namespace Kings.Score.Scoring;

/// <summary>
/// Fonction de points v0 — GRADUÉE, PROPOSITION soumise à validation (garde-fou C7).
/// Note chaque domaine sur PLUSIEURS sous-réglages avec points PARTIELS (pas de tout-ou-rien) : les
/// leviers numériques (timer, services, démarrage, espace, températures) suivent une rampe continue,
/// les booléens valent 0/plein. Un domaine n'atteint 100 que si TOUS ses sous-réglages sont optimaux
/// ⇒ le 100 reste rare. Lit uniquement des champs réellement détectables du contrat (voir
/// proposals/score-levers.md) ; aucune mesure inventée. Aucun arrondi ici (C2).
///
/// Inventaire des leviers et statuts ([Noté]/[Snapshot+]/[Catalogue]/[Non noté]) : proposals/score-levers.md.
/// Non notés volontairement : FPS (affiché si mesuré), input lag (estimation), charge mémoire
/// transitoire, puissance matérielle brute. Le barème PROFOND se figera AVEC le catalogue réel.
///
/// ⚠️ À valider / compléter avec le catalogue : HAGS (config-dépendant, non noté), la liste des
/// services « superflus », les seuils des rampes, et le score achievable précis (leviers
/// auto-applicables vs BIOS/manuel).
/// </summary>
public sealed class PointsFunctionV1 : IPointsFunction
{
    public string Version => "1.0.0";

    public DomainPoints Evaluate(SubscoresDomain domain, SystemSnapshot snapshot)
    {
        var s = snapshot.SettingsState;
        return domain switch
        {
            SubscoresDomain.Gpu => Score(
                Bool(60m, s.Gpu.DriverProfileApplied),
                Bool(40m, !string.IsNullOrWhiteSpace(s.Gpu.VendorPerfProfile))),

            SubscoresDomain.Cpu => Score(
                Bool(40m, s.Cpu.BoostEnabled),
                Graded(40m, PowerPlanFactor(s.Cpu.PowerPlan)),
                Bool(20m, s.Cpu.PboActive, applicable: snapshot.Hardware.Cpu.Vendor == CpuVendor.AMD)),

            SubscoresDomain.System => Score(
                Graded(40m, Ramp(s.System.TimerResolutionMs, worst: 15.6, best: 1.0)),
                Graded(30m, Ramp(s.System.SuperfluousServicesRunning, worst: 10, best: 0)),
                Graded(30m, Ramp(s.System.StartupProgramsCount, worst: 20, best: 0))),

            SubscoresDomain.Ram => Score(
                Bool(100m, s.Ram.XmpExpoActive, applicable: XmpProfileAvailable(snapshot))),

            SubscoresDomain.Storage => Score(
                Bool(40m, s.Storage.TrimEnabled, applicable: HasSsdOrNvme(snapshot)),
                Bool(25m, !s.Storage.IndexingOnSystemDrive),
                Graded(25m, Ramp(s.Storage.FreeSpacePct, worst: 5, best: 20)),
                Graded(10m, SmartFactor(SystemDriveSmart(snapshot)),
                    applicable: SystemDriveSmart(snapshot) != StorageSmartHealth.Unknown)),

            SubscoresDomain.Network => Score(
                Bool(60m, s.Network.TcpOptimized),
                Bool(40m, s.Network.GameDnsSet)),

            SubscoresDomain.Thermal => EvaluateThermal(snapshot),

            _ => throw new ArgumentOutOfRangeException(nameof(domain), domain, "Domaine inconnu."),
        };
    }

    private static DomainPoints EvaluateThermal(SystemSnapshot snapshot)
    {
        // Neutralisé si aucun capteur exploitable : jamais inventer une température.
        if (!snapshot.SettingsState.Thermal.Measurable)
            return DomainPoints.Neutralized();

        var m = snapshot.Metrics;
        return Score(
            Bool(40m, !snapshot.SettingsState.Thermal.ThrottlingDetected),
            Graded(30m, Ramp(m.CpuTempLoadC ?? 0d, worst: 90, best: 70), applicable: m.CpuTempLoadC.HasValue),
            Graded(30m, Ramp(m.GpuTempLoadC ?? 0d, worst: 85, best: 65), applicable: m.GpuTempLoadC.HasValue));
    }

    // ----- Mécanique de notation graduée -----

    private readonly record struct Criterion(decimal Max, decimal Fraction, bool Applicable);

    private static Criterion Bool(decimal max, bool ok, bool applicable = true)
        => new(max, ok ? 1m : 0m, applicable);

    private static Criterion Graded(decimal max, double fraction01, bool applicable = true)
        => new(max, (decimal)Math.Clamp(fraction01, 0d, 1d), applicable);

    private static DomainPoints Score(params Criterion[] criteria)
    {
        decimal maxSafe = 0m, obtained = 0m;
        foreach (var c in criteria)
        {
            if (!c.Applicable) continue;
            maxSafe += c.Max;
            obtained += c.Max * c.Fraction;
        }
        return DomainPoints.Measured(obtained, maxSafe);
    }

    /// <summary>Rampe linéaire : 0 au pire, 1 au meilleur (best peut être &lt; worst si « plus bas = mieux »).</summary>
    private static double Ramp(double value, double worst, double best)
    {
        if (worst == best) return value == best ? 1d : 0d;
        return Math.Clamp((value - worst) / (best - worst), 0d, 1d);
    }

    private static double PowerPlanFactor(string? powerPlan)
    {
        if (string.IsNullOrWhiteSpace(powerPlan)) return 0d;
        var p = powerPlan.Trim();
        if (Eq(p, "High performance") || Eq(p, "Ultimate Performance") || Eq(p, "Ultimate")) return 1.0d;
        if (Eq(p, "Balanced")) return 0.3d;
        return 0.0d; // Économie d'énergie / inconnu
    }

    private static double SmartFactor(StorageSmartHealth health) => health switch
    {
        StorageSmartHealth.OK => 1.0d,
        StorageSmartHealth.Warning => 0.5d,
        StorageSmartHealth.Critical => 0.0d,
        _ => 0.0d,
    };

    private static StorageSmartHealth SystemDriveSmart(SystemSnapshot s)
    {
        var drive = s.Hardware.Storage.FirstOrDefault(d => d.IsSystemDrive) ?? s.Hardware.Storage.FirstOrDefault();
        return drive?.SmartHealth ?? StorageSmartHealth.Unknown;
    }

    private static bool HasSsdOrNvme(SystemSnapshot s)
        => s.Hardware.Storage.Any(d => d.Type is StorageType.SSD or StorageType.NVMe);

    private static bool XmpProfileAvailable(SystemSnapshot s)
        => s.Hardware.Ram.Modules.Any(m => m.XmpExpoProfileAvailable);

    private static bool Eq(string a, string b) => a.Equals(b, StringComparison.OrdinalIgnoreCase);
}
