using Kings.Score.Contracts.Snapshot;
using Kings.Score.Scoring;

namespace KingsScore.Tests;

/// <summary>
/// Vérifie que l'échelle de score « a du sens » : une machine pleinement réglée (tous les leviers
/// sûrs actifs) tend vers 100, une machine négligée est nettement plus basse, et l'écart actuel →
/// atteignable est positif. Démontre le haut de l'échelle (les fixtures, peu réglées, montrent le bas).
/// </summary>
public sealed class ScoreScaleTests
{
    private static readonly Guid ScoreId = Guid.Parse("99999999-9999-9999-9999-999999999999");
    private static readonly DateTimeOffset ComputedAt = DateTimeOffset.Parse("2026-06-19T10:00:01Z");

    [Theory]
    [InlineData("fixture_no_thermal_sensor")]
    [InlineData("fixture_nvidia_intel_highend")]
    public void Fully_tuned_machine_scores_100(string name)
    {
        var snapshot = Fixtures.Load(name).Snapshot;
        MakeOptimal(snapshot);

        var result = new ScoreEngine().Compute(snapshot, ScoreId, ComputedAt);

        Assert.Equal(100, result.Global);
        Assert.Equal(100, result.Achievable);
    }

    [Theory]
    [InlineData("fixture_no_thermal_sensor")]
    [InlineData("fixture_nvidia_intel_highend")]
    public void Tuned_scores_higher_than_neglected_and_gap_is_positive(string name)
    {
        var engine = new ScoreEngine();

        var neglected = engine.Compute(Fixtures.Load(name).Snapshot, ScoreId, ComputedAt);

        var tunedSnapshot = Fixtures.Load(name).Snapshot;
        MakeOptimal(tunedSnapshot);
        var tuned = engine.Compute(tunedSnapshot, ScoreId, ComputedAt);

        Assert.True(tuned.Global > neglected.Global, "Le réglage doit augmenter le score.");
        Assert.True(neglected.Achievable > neglected.Global, "L'écart actuel → atteignable doit être positif.");
    }

    /// <summary>Force tous les réglages de <c>settingsState</c> à leur valeur optimale selon les
    /// critères v0 (+ marges thermiques saines), pour atteindre le haut de l'échelle.</summary>
    private static void MakeOptimal(SystemSnapshot s)
    {
        var ss = s.SettingsState;
        ss.Gpu.DriverProfileApplied = true;
        ss.Cpu.BoostEnabled = true;
        ss.Cpu.PowerPlan = "High performance";
        ss.System.TimerResolutionMs = 0.5;
        ss.System.SuperfluousServicesRunning = 0;
        ss.System.StartupProgramsCount = 0;
        ss.Ram.XmpExpoActive = true;
        ss.Storage.TrimEnabled = true;
        ss.Storage.IndexingOnSystemDrive = false;
        ss.Storage.FreeSpacePct = 60;
        ss.Network.TcpOptimized = true;
        ss.Network.GameDnsSet = true;
        ss.Thermal.ThrottlingDetected = false;
        // Marges thermiques saines (utile seulement si le domaine thermique est mesurable).
        s.Metrics.CpuTempLoadC = 60;
        s.Metrics.GpuTempLoadC = 60;
    }
}
