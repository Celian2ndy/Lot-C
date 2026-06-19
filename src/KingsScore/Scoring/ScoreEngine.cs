using Kings.Score.Contracts.Score;
using Kings.Score.Contracts.Snapshot;

namespace Kings.Score.Scoring;

/// <summary>
/// Le MOTEUR DE SCORE : transforme une photographie de machine (<see cref="SystemSnapshot"/>) en un
/// <see cref="ScoreResult"/> reproductible. Bibliothèque PURE (C9) : aucune dépendance à l'heure / au
/// hasard / à un état externe / à l'ordre d'itération non garanti (C1).
///
/// Déterminisme : <c>scoreId</c> et <c>computedAt</c> sont des métadonnées NON déterministes ;
/// l'appelant les injecte (ils ne sont jamais générés ici). Le test de reproductibilité compare le
/// reste (global, achievable, subscores), comme le précisent les fixtures (« hors scoreId/computedAt »).
/// </summary>
public sealed class ScoreEngine
{
    /// <summary>Version de schéma de l'objet ScoreResult (cf. fixtures : schemaVersion = "1.0.0").</summary>
    public const string ScoreResultSchemaVersion = "1.0.0";

    private readonly ScoringModel _model;

    public ScoreEngine(ScoringModel? model = null) => _model = model ?? ScoringModel.V1;

    /// <summary>Calcule le ScoreResult complet, avec les métadonnées injectées par l'appelant.</summary>
    public ScoreResult Compute(SystemSnapshot snapshot, Guid scoreId, DateTimeOffset computedAt)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var core = ComputeCore(snapshot);

        return new ScoreResult
        {
            ScoreId = scoreId,
            SnapshotId = snapshot.SnapshotId,
            SchemaVersion = ScoreResultSchemaVersion,
            Global = core.Global,
            Achievable = core.Achievable,
            WeightsetVersion = _model.Version,
            ComputedAt = computedAt,
            Subscores = core.Subscores,
        };
    }

    /// <summary>Partie purement déterministe : sous-scores + global + achievable, sans métadonnées.</summary>
    public ScoreCore ComputeCore(SystemSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var weightset = _model.Weightset;

        // 1) Points par domaine (ordre canonique du barème = déterministe).
        var points = new Dictionary<SubscoresDomain, DomainPoints>();
        foreach (var domain in weightset.Order)
            points[domain] = _model.PointsFunction.Evaluate(domain, snapshot);

        // 2) Domaines mesurables + dénominateur de redistribution (pleine précision).
        var measurable = weightset.Order.Where(d => points[d].Measurable).ToList();
        var measurableBaseSum = weightset.MeasurableBaseSum(measurable);
        if (measurableBaseSum <= 0m)
            throw new InvalidOperationException("Aucun domaine mesurable : score impossible.");

        // 3) Sous-scores + accumulation du global / achievable en PLEINE précision (C2).
        var subscores = new List<Subscores>(weightset.Order.Count);
        decimal weightedNormalizedSum = 0m;   // Σ base_d × normalized_d (mesurables)
        decimal weightedAchievableSum = 0m;    // Σ base_d × achievableNormalized_d (mesurables)

        foreach (var domain in weightset.Order)
        {
            var p = points[domain];

            if (!p.Measurable)
            {
                subscores.Add(new Subscores
                {
                    Domain = domain,
                    PointsObtained = 0,
                    PointsMaxSafe = 0,
                    Normalized = 0,
                    Weight = 0,
                    Neutralized = true,
                });
                continue;
            }

            var normalized = p.PointsMaxSafe == 0m ? 100m : 100m * p.PointsObtained / p.PointsMaxSafe;
            // v0 : on suppose le maximum sûr atteignable (achievable = 100 par domaine mesurable).
            // ⚠️ Le achievable PRÉCIS dépend du catalogue (leviers auto-applicables) — non fourni.
            var achievableNormalized = 100m;

            var baseWeight = weightset.BaseWeight(domain);
            weightedNormalizedSum += baseWeight * normalized;
            weightedAchievableSum += baseWeight * achievableNormalized;

            var effectiveWeight = weightset.EffectiveWeight(domain, measurableBaseSum);

            subscores.Add(new Subscores
            {
                Domain = domain,
                PointsObtained = (double)p.PointsObtained,
                PointsMaxSafe = (double)p.PointsMaxSafe,
                Normalized = (double)Display(normalized),
                Weight = (double)Display(effectiveWeight),
                Neutralized = false,
            });
        }

        var global = ToScore(weightedNormalizedSum / measurableBaseSum);
        var achievable = ToScore(weightedAchievableSum / measurableBaseSum);

        return new ScoreCore(global, achievable, subscores);
    }

    /// <summary>Arrondi d'AFFICHAGE (1 décimale). Jamais utilisé dans le calcul interne du global (C2).</summary>
    private static decimal Display(decimal value) => Math.Round(value, 1, MidpointRounding.AwayFromZero);

    /// <summary>Convertit un score interne (0–100, pleine précision) en entier borné [0,100].</summary>
    private static int ToScore(decimal value)
    {
        var rounded = (int)Math.Round(value, 0, MidpointRounding.AwayFromZero);
        return Math.Clamp(rounded, 0, 100);
    }
}

/// <summary>Partie déterministe d'un calcul de score (sans <c>scoreId</c>/<c>computedAt</c>).</summary>
public sealed record ScoreCore(int Global, int Achievable, IReadOnlyList<Subscores> Subscores);
