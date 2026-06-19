using Kings.Score.Catalog;
using Kings.Score.Contracts.Snapshot;
using Kings.Score.Scoring;
using Kings.Score.Selection;

namespace KingsScore.Tests;

/// <summary>Moteur de sélection : plan d'optimisation (sans incompatibilités) + proposition OC veryLow.</summary>
public sealed class SelectionTests
{
    private static readonly Guid PlanId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid ProposalId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    private static SystemSnapshot Snap(string name = "fixture_nvidia_intel_highend")
        => Fixtures.Load(name).Snapshot;

    // ===================== Plan =====================

    [Fact]
    public void Plan_includes_only_applicable_tweaks()
    {
        // high-end : TRIM déjà actif (non applicable) ; powerPlan Balanced + timer 15.6 ms applicables.
        var plan = new SelectionEngine(TestCatalogs.Seed).BuildPlan(Snap(), ocOptIn: false, PlanId);

        var ids = plan.Steps.Select(s => s.TweakId).ToList();
        Assert.Contains("cpu.powerplan.highperf", ids);
        Assert.Contains("system.timer.resolution", ids);
        Assert.DoesNotContain("storage.trim.enable", ids); // déjà actif => non applicable
    }

    [Fact]
    public void Plan_includes_trim_when_applicable()
    {
        var snap = Snap();
        snap.SettingsState.Storage.TrimEnabled = false; // rend TRIM applicable
        var plan = new SelectionEngine(TestCatalogs.Seed).BuildPlan(snap, ocOptIn: false, PlanId);

        Assert.Contains("storage.trim.enable", plan.Steps.Select(s => s.TweakId));
    }

    [Fact]
    public void Plan_excludes_incompatibilities()
    {
        var a = TestCatalogs.TimerResolution;                              // applicable (timer 15.6)
        var b = a with { Id = "system.timer.alt", ApplyEffect = null };    // applicable aussi
        var catalog = new TestCatalog
        {
            Tweaks = new[] { a, b },
            Incompatibilities = new[] { new Incompatibility(a.Id, b.Id, "Deux réglages du même timer.") },
        };

        var plan = new SelectionEngine(catalog).BuildPlan(Snap(), ocOptIn: false, PlanId);

        var timerSteps = plan.Steps.Where(s => s.TweakId is "system.timer.resolution" or "system.timer.alt").ToList();
        Assert.Single(timerSteps);
        // Ordre déterministe par id ordinal : "system.timer.alt" précède "system.timer.resolution" et l'emporte.
        Assert.Equal("system.timer.alt", timerSteps[0].TweakId);
    }

    [Fact]
    public void Plan_estimated_score_after_beats_current()
    {
        var snap = Snap();
        var current = new ScoreEngine().ComputeCore(snap).Global;
        var plan = new SelectionEngine(TestCatalogs.Seed).BuildPlan(snap, ocOptIn: false, PlanId);

        Assert.True(plan.EstimatedScoreAfter > current,
            $"estimatedScoreAfter {plan.EstimatedScoreAfter} doit dépasser le score actuel {current}.");
        Assert.InRange(plan.EstimatedScoreAfter, 0, 100);
    }

    [Fact]
    public void Plan_steps_are_ordered_sequentially()
    {
        var plan = new SelectionEngine(TestCatalogs.Seed).BuildPlan(Snap(), ocOptIn: true, PlanId);

        for (var i = 0; i < plan.Steps.Count; i++)
            Assert.Equal(i, plan.Steps[i].Order);
        Assert.True(plan.OcOptIn);
        Assert.Equal(Snap().SnapshotId, plan.SnapshotId);
    }

    // ===================== Overclocking =====================

    [Fact]
    public void Oc_proposal_is_empty_when_no_oc_tweaks()
    {
        // Catalogue seed = aucun réglage OC => proposition VIDE mais éligible (desktop, BIOS ouvert).
        var oc = new SelectionEngine(TestCatalogs.Seed).BuildOcProposal(Snap(), ProposalId);

        Assert.True(oc.Eligible);
        Assert.Null(oc.IneligibilityReason);
        Assert.Empty(oc.Steps);
        Assert.Equal(0d, oc.EstimatedGainPct);
        Assert.True(oc.RequiresStabilityTest);
    }

    [Fact]
    public void Oc_is_ineligible_for_laptop()
    {
        var snap = Snap();
        snap.Hardware.Chassis = HardwareChassis.Laptop;
        var catalog = new TestCatalog { Tweaks = new[] { TestCatalogs.Oc("oc.gpu.mild", 3m) } };

        var oc = new SelectionEngine(catalog).BuildOcProposal(snap, ProposalId);

        Assert.False(oc.Eligible);
        Assert.False(string.IsNullOrWhiteSpace(oc.IneligibilityReason));
        Assert.Empty(oc.Steps);
    }

    [Fact]
    public void Oc_is_ineligible_for_locked_bios()
    {
        var snap = Snap();
        snap.Hardware.Motherboard.BiosLocked = true;
        var catalog = new TestCatalog { Tweaks = new[] { TestCatalogs.Oc("oc.gpu.mild", 3m) } };

        var oc = new SelectionEngine(catalog).BuildOcProposal(snap, ProposalId);

        Assert.False(oc.Eligible);
        Assert.Empty(oc.Steps);
    }

    [Fact]
    public void Oc_keeps_only_veryLow()
    {
        var catalog = new TestCatalog
        {
            Tweaks = new[]
            {
                TestCatalogs.Oc("oc.safe", 2m, CatalogRiskLevel.VeryLow),
                TestCatalogs.Oc("oc.risky", 10m, CatalogRiskLevel.Low), // niveau supérieur : jamais proposé
            },
        };

        var oc = new SelectionEngine(catalog).BuildOcProposal(Snap(), ProposalId);

        var ids = oc.Steps.Select(s => s.TweakId).ToList();
        Assert.Contains("oc.safe", ids);
        Assert.DoesNotContain("oc.risky", ids);
        Assert.All(oc.Steps, s => Assert.Equal("veryLow", s.RiskLevel));
        Assert.Equal(2d, oc.EstimatedGainPct);
    }

    [Fact]
    public void Oc_maximizes_gain_respecting_incompatibilities()
    {
        // gpu(3) incompatible avec cpu(4) ; ram(2) compatible avec tout.
        // Meilleur paquet réalisable = {cpu(4), ram(2)} = 6 (vs {gpu(3), ram(2)} = 5).
        var catalog = new TestCatalog
        {
            Tweaks = new[]
            {
                TestCatalogs.Oc("oc.gpu.mild", 3m),
                TestCatalogs.Oc("oc.cpu.mild", 4m),
                TestCatalogs.Oc("oc.ram.xmp", 2m),
            },
            Incompatibilities = new[] { new Incompatibility("oc.gpu.mild", "oc.cpu.mild", "Budget thermique partagé.") },
        };

        var oc = new SelectionEngine(catalog).BuildOcProposal(Snap(), ProposalId);

        var ids = oc.Steps.Select(s => s.TweakId).OrderBy(x => x).ToList();
        Assert.Equal(new[] { "oc.cpu.mild", "oc.ram.xmp" }, ids);
        Assert.Equal(6d, oc.EstimatedGainPct);
        Assert.All(oc.Steps, s => Assert.True(s.RollbackOnInstability));
        Assert.All(oc.Steps, s => Assert.Equal($"revert:{s.TweakId}", s.RevertExact));
    }
}
