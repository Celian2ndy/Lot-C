using Kings.Score.Contracts.Score;
using Kings.Score.Scoring;

namespace KingsScore.Tests;

/// <summary>
/// LE TEST CLÉ de la redistribution (garde-fou C2). Quand le domaine thermique est neutralisé, son
/// poids (14) est redistribué au prorata sur les 6 autres ; la somme reste 100 en PLEINE précision.
/// On n'arrondit qu'à l'affichage, et aucun domaine ne « tasse » l'écart d'arrondi.
/// </summary>
public sealed class WeightRedistributionTests
{
    private static readonly Guid ScoreId = Guid.Parse("99999999-9999-9999-9999-999999999999");
    private static readonly DateTimeOffset ComputedAt = DateTimeOffset.Parse("2026-06-19T10:00:01Z");

    private static readonly SubscoresDomain[] Measurable =
    {
        SubscoresDomain.Gpu, SubscoresDomain.Cpu, SubscoresDomain.System,
        SubscoresDomain.Ram, SubscoresDomain.Network, SubscoresDomain.Storage,
    };

    [Fact]
    public void No_thermal_sensor_neutralizes_thermal()
    {
        var fx = Fixtures.Load("fixture_no_thermal_sensor");
        var result = new ScoreEngine().Compute(fx.Snapshot, ScoreId, ComputedAt);
        var thermal = result.Subscores.Single(s => s.Domain == SubscoresDomain.Thermal);

        Assert.True(thermal.Neutralized);
        Assert.Equal(0d, thermal.Weight);
        Assert.Equal(0d, thermal.PointsObtained);
        Assert.Equal(0d, thermal.PointsMaxSafe);
        Assert.Equal(0d, thermal.Normalized);
    }

    [Fact]
    public void No_thermal_sensor_display_weights_match_fixture_redistribution()
    {
        var fx = Fixtures.Load("fixture_no_thermal_sensor");
        var result = new ScoreEngine().Compute(fx.Snapshot, ScoreId, ComputedAt);
        var byDomain = result.Subscores.ToDictionary(s => s.Domain);
        var expected = fx.Root["expectedRedistribution"]!;

        AssertDisplayWeight(expected, "gpu", byDomain[SubscoresDomain.Gpu]);
        AssertDisplayWeight(expected, "cpu", byDomain[SubscoresDomain.Cpu]);
        AssertDisplayWeight(expected, "system", byDomain[SubscoresDomain.System]);
        AssertDisplayWeight(expected, "ram", byDomain[SubscoresDomain.Ram]);
        AssertDisplayWeight(expected, "network", byDomain[SubscoresDomain.Network]);
        AssertDisplayWeight(expected, "storage", byDomain[SubscoresDomain.Storage]);

        // La somme des poids d'affichage (1 décimale) vaut exactement 100,0.
        var displaySum = result.Subscores.Sum(s => (decimal)s.Weight);
        Assert.Equal(100.0m, displaySum);
    }

    [Fact]
    public void Redistribution_preserves_100_in_full_precision()
    {
        var ws = Weightset.V1;
        var s = ws.MeasurableBaseSum(Measurable);
        Assert.Equal(86m, s);

        // Pleine précision : la somme des poids effectifs vaut 100 (résidu de division décimale ~1e-26).
        var fullPrecisionSum = Measurable.Sum(d => ws.EffectiveWeight(d, s));
        Assert.Equal(100m, Math.Round(fullPrecisionSum, 20));

        // Chaque poids suit EXACTEMENT la formule base × 100 / S : aucun domaine n'absorbe l'arrondi.
        foreach (var d in Measurable)
            Assert.Equal(ws.BaseWeight(d) * 100m / s, ws.EffectiveWeight(d, s));
    }

    [Fact]
    public void High_end_all_measurable_weights_equal_base_summing_to_100()
    {
        var fx = Fixtures.Load("fixture_nvidia_intel_highend");
        var result = new ScoreEngine().Compute(fx.Snapshot, ScoreId, ComputedAt);
        var byDomain = result.Subscores.ToDictionary(s => s.Domain);

        Assert.All(result.Subscores, sub => Assert.False(sub.Neutralized));
        Assert.Equal(24d, byDomain[SubscoresDomain.Gpu].Weight);
        Assert.Equal(18d, byDomain[SubscoresDomain.Cpu].Weight);
        Assert.Equal(16d, byDomain[SubscoresDomain.System].Weight);
        Assert.Equal(14d, byDomain[SubscoresDomain.Thermal].Weight);
        Assert.Equal(12d, byDomain[SubscoresDomain.Ram].Weight);
        Assert.Equal(10d, byDomain[SubscoresDomain.Network].Weight);
        Assert.Equal(6d, byDomain[SubscoresDomain.Storage].Weight);

        Assert.Equal(100.0m, result.Subscores.Sum(sub => (decimal)sub.Weight));
    }

    private static void AssertDisplayWeight(System.Text.Json.Nodes.JsonNode expected, string key, Subscores subscore)
        => Assert.Equal(expected[key]!.GetValue<decimal>(), (decimal)subscore.Weight);
}
