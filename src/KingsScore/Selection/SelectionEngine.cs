using System.Text.Json;
using Kings.Score.Catalog;
using Kings.Score.Contracts.Score;
using Kings.Score.Contracts.Snapshot;
using Kings.Score.Json;
using Kings.Score.Scoring;
using PlanContract = Kings.Score.Contracts.Plan;
using OcContract = Kings.Score.Contracts.Oc;

namespace Kings.Score.Selection;

/// <summary>
/// Le MOTEUR DE SÉLECTION (Lot C) : compose un <see cref="PlanContract.OptimizationPlan"/> (réglages
/// ordonnés, sans incompatibilités) et une <see cref="OcContract.OcProposal"/> d'overclocking limitée
/// aux réglages <c>veryLow</c> pour la config détectée, maximisant le gain dans cette zone (C4).
/// Déterministe : aucune dépendance à l'heure / au hasard. Les identifiants (planId/proposalId) sont
/// injectés par l'appelant. Le contenu du catalogue est un livrable humain (C5) ; ici on code le moteur.
/// </summary>
public sealed class SelectionEngine
{
    private const int MaxExactSearch = 20; // garde-fou anti-explosion combinatoire pour l'OC.

    private readonly ICatalog _catalog;
    private readonly ScoreEngine _scoreEngine;

    public SelectionEngine(ICatalog catalog, ScoreEngine? scoreEngine = null)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _scoreEngine = scoreEngine ?? new ScoreEngine();
    }

    // ===================== Plan d'optimisation (1-clic) =====================

    public PlanContract.OptimizationPlan BuildPlan(SystemSnapshot snapshot, bool ocOptIn, Guid planId)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        // Réglages NON-OC applicables à cette config/état, dans un ordre déterministe.
        var applicable = _catalog.Tweaks
            .Where(t => !t.IsOverclocking && t.AppliesTo(snapshot))
            .OrderBy(t => DomainRank(t.Domain))
            .ThenBy(t => t.Id, StringComparer.Ordinal)
            .ToList();

        var selected = FilterIncompatibilities(applicable);
        var estimatedScoreAfter = EstimateScoreAfter(snapshot, selected);

        var steps = selected
            .Select((t, i) => new PlanContract.Steps
            {
                Order = i,
                TweakId = t.Id,
                Domain = ToPlanDomain(t.Domain),
                RiskLevel = ToPlanRisk(t.RiskLevel),
                Critical = t.Critical,
                RequiresRestart = t.RequiresRestart,
            })
            .ToList();

        return new PlanContract.OptimizationPlan
        {
            PlanId = planId,
            SnapshotId = snapshot.SnapshotId,
            OcOptIn = ocOptIn,
            EstimatedScoreAfter = estimatedScoreAfter,
            Steps = steps,
        };
    }

    // ===================== Proposition d'overclocking (opt-in) =====================

    public OcContract.OcProposal BuildOcProposal(SystemSnapshot snapshot, Guid proposalId)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var (eligible, reason) = EvaluateEligibility(snapshot);
        if (!eligible)
            return EmptyProposal(snapshot, proposalId, eligible: false, reason);

        // Seuls les réglages OC veryLow applicables à la config détectée (C4). Jamais relever un niveau.
        var candidates = _catalog.Tweaks
            .Where(t => t.IsOverclocking
                        && t.RiskLevel == CatalogRiskLevel.VeryLow
                        && t.AppliesTo(snapshot))
            .ToList();

        // Aucun veryLow disponible => proposition VIDE (mais éligible).
        if (candidates.Count == 0)
            return EmptyProposal(snapshot, proposalId, eligible: true, reason: null);

        // Paquet maximisant le gain dans la zone veryLow, sans incompatibilités.
        var package = MaxGainPackage(candidates);

        var steps = package
            .OrderBy(t => t.Id, StringComparer.Ordinal)
            .Select(t => new OcContract.Steps
            {
                TweakId = t.Id,
                RiskLevel = "veryLow",
                ExpectedGainPct = (double)t.ExpectedGainPct,
                RevertExact = t.RevertExact,
                RollbackOnInstability = true,
            })
            .ToList();

        return new OcContract.OcProposal
        {
            ProposalId = proposalId,
            SnapshotId = snapshot.SnapshotId,
            Eligible = true,
            IneligibilityReason = null,
            EstimatedGainPct = (double)package.Sum(t => t.ExpectedGainPct),
            RequiresStabilityTest = true, // toujours vrai en v1
            Steps = steps,
        };
    }

    private static OcContract.OcProposal EmptyProposal(SystemSnapshot snapshot, Guid proposalId, bool eligible, string? reason)
        => new()
        {
            ProposalId = proposalId,
            SnapshotId = snapshot.SnapshotId,
            Eligible = eligible,
            IneligibilityReason = reason,
            EstimatedGainPct = 0,
            RequiresStabilityTest = true,
            Steps = new List<OcContract.Steps>(),
        };

    private static (bool Eligible, string? Reason) EvaluateEligibility(SystemSnapshot snapshot)
    {
        // Prudence (promesse n°1, « en cas de doute on n'applique pas ») : seul un desktop confirmé,
        // BIOS non verrouillé, est éligible. Laptop et châssis indéterminé sont refusés et motivés.
        var chassis = snapshot.Hardware.Chassis;
        if (chassis == HardwareChassis.Laptop)
            return (false, "Machine portable : overclocking non éligible en v1.");
        if (chassis != HardwareChassis.Desktop)
            return (false, "Châssis indéterminé : par prudence, overclocking non éligible.");
        if (snapshot.Hardware.Motherboard.BiosLocked)
            return (false, "BIOS verrouillé (OEM) : overclocking non éligible.");
        return (true, null);
    }

    // ===================== Mécanique commune =====================

    /// <summary>Garde un ensemble sans incompatibilités (greedy : le premier dans l'ordre l'emporte).</summary>
    private List<CatalogTweak> FilterIncompatibilities(IReadOnlyList<CatalogTweak> ordered)
    {
        var kept = new List<CatalogTweak>();
        foreach (var t in ordered)
        {
            var conflicts = kept.Any(k => _catalog.Incompatibilities.Any(inc => inc.IsPair(k.Id, t.Id)));
            if (!conflicts) kept.Add(t);
        }
        return kept;
    }

    /// <summary>Sous-ensemble réalisable (sans incompatibilités) maximisant le gain estimé.
    /// Recherche exacte pour les petits ensembles, repli greedy au-delà (rare).</summary>
    private List<CatalogTweak> MaxGainPackage(List<CatalogTweak> items)
    {
        if (items.Count == 0) return new();
        if (items.Count > MaxExactSearch) return GreedyByGain(items);

        // Ordre stable pour un départage déterministe.
        items = items.OrderBy(t => t.Id, StringComparer.Ordinal).ToList();

        List<CatalogTweak> best = new();
        decimal bestGain = 0m; // l'ensemble vide (gain 0) est la borne basse.

        var n = items.Count;
        for (var mask = 1; mask < (1 << n); mask++)
        {
            var subset = new List<CatalogTweak>();
            for (var i = 0; i < n; i++)
                if ((mask & (1 << i)) != 0) subset.Add(items[i]);

            if (!IsFeasible(subset)) continue;

            var gain = subset.Sum(t => t.ExpectedGainPct);
            if (gain > bestGain || (gain == bestGain && LexLess(subset, best)))
            {
                best = subset;
                bestGain = gain;
            }
        }
        return best;
    }

    private List<CatalogTweak> GreedyByGain(List<CatalogTweak> items)
    {
        var kept = new List<CatalogTweak>();
        foreach (var t in items.OrderByDescending(t => t.ExpectedGainPct).ThenBy(t => t.Id, StringComparer.Ordinal))
        {
            if (!kept.Any(k => _catalog.Incompatibilities.Any(inc => inc.IsPair(k.Id, t.Id))))
                kept.Add(t);
        }
        return kept;
    }

    private bool IsFeasible(IReadOnlyList<CatalogTweak> subset)
    {
        for (var i = 0; i < subset.Count; i++)
            for (var j = i + 1; j < subset.Count; j++)
                if (_catalog.Incompatibilities.Any(inc => inc.IsPair(subset[i].Id, subset[j].Id)))
                    return false;
        return true;
    }

    private static bool LexLess(List<CatalogTweak> a, List<CatalogTweak> b)
    {
        if (b.Count == 0) return a.Count > 0; // tout ensemble non vide bat l'ensemble vide à gain égal (gain 0 only)
        var ai = a.Select(t => t.Id).OrderBy(x => x, StringComparer.Ordinal).ToList();
        var bi = b.Select(t => t.Id).OrderBy(x => x, StringComparer.Ordinal).ToList();
        var len = Math.Min(ai.Count, bi.Count);
        for (var i = 0; i < len; i++)
        {
            var c = string.CompareOrdinal(ai[i], bi[i]);
            if (c != 0) return c < 0;
        }
        return ai.Count < bi.Count;
    }

    private int EstimateScoreAfter(SystemSnapshot snapshot, IReadOnlyList<CatalogTweak> selected)
    {
        if (selected.Count == 0)
            return _scoreEngine.ComputeCore(snapshot).Global;

        var clone = DeepClone(snapshot);
        foreach (var t in selected)
            t.ApplyEffect?.Invoke(clone.SettingsState);

        return _scoreEngine.ComputeCore(clone).Global;
    }

    private static SystemSnapshot DeepClone(SystemSnapshot snapshot)
    {
        var json = JsonSerializer.Serialize(snapshot, KingsJson.Options);
        return JsonSerializer.Deserialize<SystemSnapshot>(json, KingsJson.Options)!;
    }

    private static int DomainRank(SubscoresDomain domain)
    {
        var order = Weightset.V1.Order;
        for (var i = 0; i < order.Count; i++)
            if (order[i] == domain) return i;
        return int.MaxValue;
    }

    private static PlanContract.StepsDomain ToPlanDomain(SubscoresDomain domain) => domain switch
    {
        SubscoresDomain.Gpu => PlanContract.StepsDomain.Gpu,
        SubscoresDomain.Cpu => PlanContract.StepsDomain.Cpu,
        SubscoresDomain.Ram => PlanContract.StepsDomain.Ram,
        SubscoresDomain.Storage => PlanContract.StepsDomain.Storage,
        SubscoresDomain.System => PlanContract.StepsDomain.System,
        SubscoresDomain.Thermal => PlanContract.StepsDomain.Thermal,
        SubscoresDomain.Network => PlanContract.StepsDomain.Network,
        _ => throw new ArgumentOutOfRangeException(nameof(domain), domain, null),
    };

    private static PlanContract.StepsRiskLevel ToPlanRisk(CatalogRiskLevel risk) => risk switch
    {
        CatalogRiskLevel.VeryLow => PlanContract.StepsRiskLevel.VeryLow,
        CatalogRiskLevel.Low => PlanContract.StepsRiskLevel.Low,
        CatalogRiskLevel.Medium => PlanContract.StepsRiskLevel.Medium,
        CatalogRiskLevel.High => PlanContract.StepsRiskLevel.High,
        CatalogRiskLevel.VeryHigh => PlanContract.StepsRiskLevel.VeryHigh,
        _ => throw new ArgumentOutOfRangeException(nameof(risk), risk, null),
    };
}
