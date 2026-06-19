using Kings.Score.Contracts.Score;
using Kings.Score.Scoring;

namespace KingsScore.Tests;

/// <summary>Conformité structurelle du ScoreResult au contrat (forme, bornes, métadonnées).</summary>
public sealed class ScoreStructureTests
{
    private static readonly Guid ScoreId = Guid.Parse("99999999-9999-9999-9999-999999999999");
    private static readonly DateTimeOffset ComputedAt = DateTimeOffset.Parse("2026-06-19T10:00:01Z");

    private static readonly SubscoresDomain[] CanonicalOrder =
    {
        SubscoresDomain.Gpu, SubscoresDomain.Cpu, SubscoresDomain.System, SubscoresDomain.Thermal,
        SubscoresDomain.Ram, SubscoresDomain.Network, SubscoresDomain.Storage,
    };

    [Theory]
    [InlineData("fixture_no_thermal_sensor")]
    [InlineData("fixture_nvidia_intel_highend")]
    public void Result_is_structurally_conform(string name)
    {
        var fx = Fixtures.Load(name);
        var result = new ScoreEngine().Compute(fx.Snapshot, ScoreId, ComputedAt);

        Assert.Equal(7, result.Subscores.Count);
        Assert.Equal(CanonicalOrder, result.Subscores.Select(s => s.Domain));

        Assert.InRange(result.Global, 0, 100);
        Assert.InRange(result.Achievable, 0, 100);
        Assert.Equal("1.0.0", result.WeightsetVersion);
        Assert.Equal("1.0.0", result.SchemaVersion);

        Assert.Equal(fx.Snapshot.SnapshotId, result.SnapshotId);
        Assert.Equal(ScoreId, result.ScoreId);
        Assert.Equal(ComputedAt, result.ComputedAt);

        foreach (var s in result.Subscores)
        {
            Assert.InRange(s.Normalized, 0d, 100d);
            Assert.True(s.Weight >= 0d, $"Poids négatif pour {s.Domain}.");
            Assert.True(s.PointsObtained >= 0d, $"Points négatifs pour {s.Domain}.");
            Assert.True(s.PointsObtained <= s.PointsMaxSafe + 1e-9,
                $"pointsObtained > pointsMaxSafe pour {s.Domain}.");
        }

        // La marge de progression sûre est positive (score atteignable >= score actuel).
        Assert.True(result.Achievable >= result.Global);
    }
}
