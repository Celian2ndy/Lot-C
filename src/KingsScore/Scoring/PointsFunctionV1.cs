using Kings.Score.Contracts.Score;
using Kings.Score.Contracts.Snapshot;

namespace Kings.Score.Scoring;

/// <summary>
/// Fonction de points v0 — PROPOSITION soumise à validation (garde-fou C7 : « mapping D'ABORD, puis
/// on fige »). Lit l'état réel des réglages (<c>settingsState</c>) + les métriques, et attribue des
/// points par domaine selon des critères de bonne pratique. Les <c>expectedScoreResult</c> des
/// fixtures ne sont PAS encore figés : ils le seront par l'équipe une fois cette fonction validée.
///
/// Convention : <c>pointsMaxSafe</c> = somme des points des critères APPLICABLES (≈ 100 par domaine),
/// <c>pointsObtained</c> = somme des points des critères satisfaits. Aucun arrondi ici (C2).
///
/// ⚠️ Points de jugement à valider (je ne tranche pas seul) :
///   - HAGS (<c>hagsEnabled</c>) et PBO (<c>pboActive</c>) sont dépendants de la config : NON notés en v0.
///   - Seuils numériques (timer ≤ 1.0 ms, ≤ 3 services superflus, ≤ 8 programmes au démarrage,
///     marge thermique CPU &lt; 85 °C / GPU &lt; 80 °C, espace disque ≥ 15 %).
///   - Le score <c>achievable</c> précis dépend du CATALOGUE (leviers auto-applicables vs BIOS/manuel),
///     non fourni : en v0, achievable suppose tous les leviers sûrs applicables (voir ScoreEngine).
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
                Crit(100m, s.Gpu.DriverProfileApplied)),

            SubscoresDomain.Cpu => Score(
                Crit(50m, s.Cpu.BoostEnabled),
                Crit(50m, IsHighPerformancePlan(s.Cpu.PowerPlan))),

            SubscoresDomain.System => Score(
                Crit(40m, s.System.TimerResolutionMs <= 1.0),
                Crit(30m, s.System.SuperfluousServicesRunning <= 3),
                Crit(30m, s.System.StartupProgramsCount <= 8)),

            SubscoresDomain.Ram => Score(
                Crit(100m, s.Ram.XmpExpoActive, applicable: XmpProfileAvailable(snapshot))),

            SubscoresDomain.Network => Score(
                Crit(50m, s.Network.TcpOptimized),
                Crit(50m, s.Network.GameDnsSet)),

            SubscoresDomain.Storage => Score(
                Crit(50m, s.Storage.TrimEnabled),
                Crit(30m, !s.Storage.IndexingOnSystemDrive),
                Crit(20m, s.Storage.FreeSpacePct >= 15)),

            SubscoresDomain.Thermal => EvaluateThermal(snapshot),

            _ => throw new ArgumentOutOfRangeException(nameof(domain), domain, "Domaine inconnu."),
        };
    }

    private static DomainPoints EvaluateThermal(SystemSnapshot snapshot)
    {
        // Neutralisé si le domaine thermique n'est pas mesurable (aucun capteur exploitable).
        if (!snapshot.SettingsState.Thermal.Measurable)
            return DomainPoints.Neutralized();

        var m = snapshot.Metrics;
        var cpuLoad = m.CpuTempLoadC;
        var gpuLoad = m.GpuTempLoadC;
        var marginApplicable = cpuLoad.HasValue && gpuLoad.HasValue;
        var marginOk = marginApplicable && cpuLoad!.Value < 85 && gpuLoad!.Value < 80;

        return Score(
            Crit(50m, !snapshot.SettingsState.Thermal.ThrottlingDetected),
            Crit(50m, marginOk, applicable: marginApplicable));
    }

    /// <summary>Un critère noté : <paramref name="points"/> obtenus si <paramref name="satisfied"/>,
    /// comptant dans le max sûr seulement si <paramref name="applicable"/> à cette config.</summary>
    private static (decimal Points, bool Satisfied, bool Applicable) Crit(decimal points, bool satisfied, bool applicable = true)
        => (points, satisfied, applicable);

    private static DomainPoints Score(params (decimal Points, bool Satisfied, bool Applicable)[] criteria)
    {
        decimal maxSafe = 0m, obtained = 0m;
        foreach (var c in criteria)
        {
            if (!c.Applicable) continue;
            maxSafe += c.Points;
            if (c.Satisfied) obtained += c.Points;
        }
        return DomainPoints.Measured(obtained, maxSafe);
    }

    private static bool IsHighPerformancePlan(string? powerPlan)
    {
        if (string.IsNullOrWhiteSpace(powerPlan)) return false;
        var p = powerPlan.Trim();
        return p.Equals("High performance", StringComparison.OrdinalIgnoreCase)
            || p.Equals("Ultimate Performance", StringComparison.OrdinalIgnoreCase)
            || p.Equals("Ultimate", StringComparison.OrdinalIgnoreCase);
    }

    private static bool XmpProfileAvailable(SystemSnapshot snapshot)
        => snapshot.Hardware.Ram.Modules.Any(mod => mod.XmpExpoProfileAvailable);
}
