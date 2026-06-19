using Kings.Score.Contracts.Score;

namespace Kings.Score.Scoring;

/// <summary>
/// Le BARÈME : le jeu de poids par domaine (décision produit D6, figée).
/// Séparé de la fonction de points (C8) pour pouvoir faire évoluer l'un sans l'autre.
///
/// Les poids sont des <see cref="decimal"/> EXACTS. La redistribution (quand un domaine est
/// neutralisé) ne matérialise jamais de poids pré-divisé : on conserve le poids de base + le
/// dénominateur des domaines mesurables, et on ne divise qu'au moment de produire le score ou
/// la valeur d'affichage (C2 : pleine précision en interne, arrondi seulement à l'affichage).
/// </summary>
public sealed class Weightset
{
    private readonly IReadOnlyDictionary<SubscoresDomain, decimal> _baseWeights;

    public string Version { get; }

    /// <summary>Ordre canonique des domaines dans la sortie (ordre du barème). Déterministe.</summary>
    public IReadOnlyList<SubscoresDomain> Order { get; }

    public Weightset(string version, IReadOnlyList<(SubscoresDomain Domain, decimal Weight)> weights)
    {
        Version = version;
        Order = weights.Select(w => w.Domain).ToArray();
        _baseWeights = weights.ToDictionary(w => w.Domain, w => w.Weight);

        var total = _baseWeights.Values.Sum();
        if (total != 100m)
            throw new ArgumentException($"La somme des poids du barème doit être 100, obtenu {total}.", nameof(weights));
    }

    public decimal BaseWeight(SubscoresDomain domain) => _baseWeights[domain];

    /// <summary>Somme des poids de base des domaines mesurables (le dénominateur de redistribution).</summary>
    public decimal MeasurableBaseSum(IReadOnlyCollection<SubscoresDomain> measurableDomains)
        => measurableDomains.Sum(BaseWeight);

    /// <summary>
    /// Poids effectif (après redistribution) d'un domaine mesurable, en pleine précision décimale :
    /// poids_base × 100 / Σ(poids_base des mesurables). Pour un domaine neutralisé, le poids effectif est 0.
    /// </summary>
    public decimal EffectiveWeight(SubscoresDomain domain, decimal measurableBaseSum)
        => BaseWeight(domain) * 100m / measurableBaseSum;

    /// <summary>
    /// Barème v1 (D6) : GPU 24 · CPU 18 · Système 16 · Thermique 14 · RAM 12 · Réseau 10 · Stockage 6 = 100.
    /// Ordre = ordre du barème (= ordre des subscores dans les fixtures).
    /// </summary>
    public static Weightset V1 { get; } = new("1.0.0", new[]
    {
        (SubscoresDomain.Gpu,     24m),
        (SubscoresDomain.Cpu,     18m),
        (SubscoresDomain.System,  16m),
        (SubscoresDomain.Thermal, 14m),
        (SubscoresDomain.Ram,     12m),
        (SubscoresDomain.Network, 10m),
        (SubscoresDomain.Storage,  6m),
    });
}
