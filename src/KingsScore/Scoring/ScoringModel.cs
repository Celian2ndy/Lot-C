namespace Kings.Score.Scoring;

/// <summary>
/// Un modèle de score = un barème (poids) + une fonction de points, versionnés ENSEMBLE.
///
/// Le contrat (ScoreResult.weightsetVersion) garantit « même snapshot + même weightsetVersion ⇒ même
/// score ». Comme le score dépend des DEUX composants, <see cref="Version"/> (exposée comme
/// weightsetVersion) doit changer dès que l'un OU l'autre évolue — sinon la reproductibilité serait
/// rompue. Les composants restent séparés dans le code (C8) ; ils ne le sont pas dans le versionnement.
/// </summary>
public sealed class ScoringModel
{
    public Weightset Weightset { get; }
    public IPointsFunction PointsFunction { get; }

    /// <summary>Version du modèle de score complet (poids + points). Exposée comme <c>weightsetVersion</c>.</summary>
    public string Version { get; }

    public ScoringModel(Weightset weightset, IPointsFunction pointsFunction, string version)
    {
        Weightset = weightset;
        PointsFunction = pointsFunction;
        Version = version;
    }

    /// <summary>Modèle v1 : barème v1 (D6) + fonction de points v0 (en attente de validation, C7).</summary>
    public static ScoringModel V1 { get; } = new(Weightset.V1, new PointsFunctionV1(), "1.0.0");
}
