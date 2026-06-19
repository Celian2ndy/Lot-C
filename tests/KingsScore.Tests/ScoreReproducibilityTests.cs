using System.Text.Json;
using Kings.Score.Contracts.Score;
using Kings.Score.Scoring;

namespace KingsScore.Tests;

/// <summary>
/// LE PREMIER TEST (garde-fou C1) : reproductibilité du score. Mêmes entrées ⇒ même sortie, à
/// l'identique. Aucune dépendance à l'heure / au hasard / à l'ordre d'itération.
/// </summary>
public sealed class ScoreReproducibilityTests
{
    private static readonly Guid ScoreId = Guid.Parse("99999999-9999-9999-9999-999999999999");
    private static readonly DateTimeOffset ComputedAt = DateTimeOffset.Parse("2026-06-19T10:00:01Z");

    [Theory]
    [InlineData("fixture_no_thermal_sensor")]
    [InlineData("fixture_nvidia_intel_highend")]
    public void Same_input_same_output_byte_for_byte(string name)
    {
        var engine = new ScoreEngine();

        // Recalcul depuis un snapshot RE-désérialisé : détecte tout état partagé caché.
        var first = engine.Compute(Fixtures.Load(name).Snapshot, ScoreId, ComputedAt);
        var second = engine.Compute(Fixtures.Load(name).Snapshot, ScoreId, ComputedAt);

        Assert.Equal(Serialize(first), Serialize(second));
    }

    [Theory]
    [InlineData("fixture_no_thermal_sensor")]
    [InlineData("fixture_nvidia_intel_highend")]
    public void Score_does_not_depend_on_scoreId_or_computedAt(string name)
    {
        var snapshot = Fixtures.Load(name).Snapshot;
        var engine = new ScoreEngine();

        // Métadonnées volontairement différentes (et non déterministes) : le SCORE doit être identique.
        var a = engine.Compute(snapshot, Guid.NewGuid(), DateTimeOffset.UtcNow);
        var b = engine.Compute(snapshot, Guid.NewGuid(), DateTimeOffset.UtcNow.AddHours(7));

        Assert.Equal(a.Global, b.Global);
        Assert.Equal(a.Achievable, b.Achievable);
        Assert.Equal(SerializeSubscores(a), SerializeSubscores(b));
    }

    [Theory]
    [InlineData("fixture_no_thermal_sensor")]
    [InlineData("fixture_nvidia_intel_highend")]
    public void ComputeCore_is_stable_across_repeated_calls(string name)
    {
        var snapshot = Fixtures.Load(name).Snapshot;
        var engine = new ScoreEngine();

        var runs = Enumerable.Range(0, 5).Select(_ => engine.ComputeCore(snapshot)).ToList();
        var reference = SerializeCore(runs[0]);
        Assert.All(runs, r => Assert.Equal(reference, SerializeCore(r)));
    }

    private static string Serialize(ScoreResult r) => JsonSerializer.Serialize(r, Fixtures.Json);

    private static string SerializeSubscores(ScoreResult r) =>
        JsonSerializer.Serialize(r.Subscores, Fixtures.Json);

    private static string SerializeCore(ScoreCore c) =>
        JsonSerializer.Serialize(new { c.Global, c.Achievable, c.Subscores }, Fixtures.Json);
}
