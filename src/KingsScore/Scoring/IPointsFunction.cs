using Kings.Score.Contracts.Score;
using Kings.Score.Contracts.Snapshot;

namespace Kings.Score.Scoring;

/// <summary>
/// La FONCTION DE POINTS : combien vaut l'état réel d'un domaine, et quel est son maximum sûr de
/// référence. Séparée du barème (C8). Déterministe : lit uniquement le <see cref="SystemSnapshot"/>,
/// aucune dépendance à l'heure / au hasard / à un état externe (C1).
///
/// Versionnée : tout changement de cette fonction modifie le score et DOIT s'accompagner d'un bump
/// de version du modèle de score (voir <see cref="ScoringModel"/>), sinon la garantie de
/// reproductibilité du contrat (même weightsetVersion ⇒ même score) serait rompue.
/// </summary>
public interface IPointsFunction
{
    string Version { get; }

    /// <summary>Évalue un domaine sur l'état réel de la machine.</summary>
    DomainPoints Evaluate(SubscoresDomain domain, SystemSnapshot snapshot);
}

/// <summary>
/// Résultat de la fonction de points pour un domaine.
/// <para><see cref="Measurable"/> = false ⇒ le domaine est NEUTRALISÉ (non mesurable) : il est exclu
/// du score et son poids redistribué (p. ex. thermique sans capteur).</para>
/// </summary>
public readonly record struct DomainPoints(decimal PointsObtained, decimal PointsMaxSafe, bool Measurable)
{
    /// <summary>Domaine mesuré et noté.</summary>
    public static DomainPoints Measured(decimal obtained, decimal maxSafe) => new(obtained, maxSafe, true);

    /// <summary>Domaine non mesurable ⇒ neutralisé (poids redistribué sur les autres).</summary>
    public static DomainPoints Neutralized() => new(0m, 0m, false);
}
