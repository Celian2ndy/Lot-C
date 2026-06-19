using Kings.Score.Contracts.Score;
using Kings.Score.Contracts.Snapshot;

namespace Kings.Score.Catalog;

/// <summary>Échelle de risque à 5 niveaux (calque du contrat). En v1, seul <c>VeryLow</c> est proposé en OC.</summary>
public enum CatalogRiskLevel { VeryLow, Low, Medium, High, VeryHigh }

/// <summary>Source d'un réglage : interne ou via SDK constructeur.</summary>
public enum TweakSource { Internal, VendorSdk }

/// <summary>
/// Un réglage du catalogue, vu par le MOTEUR DE SÉLECTION (Lot C). Modèle proposé
/// (cf. proposals/tweak-descriptor.md) ; le contenu réel du catalogue est un livrable humain (C5),
/// le format du descripteur reste à figer côté humain (partagé avec Lot A).
///
/// <para><see cref="AppliesTo"/> (condition) et <see cref="ApplyEffect"/> (effet sur settingsState)
/// sont ici des délégués évaluables ; dans le vrai catalogue distribué en packs, ils seront encodés
/// en données puis « compilés » vers ces délégués. <see cref="ApplyEffect"/> sert à C pour simuler et
/// recalculer <c>estimatedScoreAfter</c> ; l'action réelle (opaque) est exécutée par A.</para>
/// </summary>
public sealed record CatalogTweak
{
    public required string Id { get; init; }
    public required SubscoresDomain Domain { get; init; }
    public required CatalogRiskLevel RiskLevel { get; init; }
    public bool Critical { get; init; }
    public bool RequiresRestart { get; init; }
    public TweakSource Source { get; init; }

    /// <summary>Vrai pour un réglage d'overclocking (flux opt-in séparé). Champ descripteur proposé.</summary>
    public bool IsOverclocking { get; init; }

    /// <summary>Condition d'applicabilité (config + état détecté). Sinon le réglage est ignoré (jamais à l'aveugle).</summary>
    public required Func<SystemSnapshot, bool> AppliesTo { get; init; }

    /// <summary>Effet simulé sur <c>settingsState</c> (pour <c>estimatedScoreAfter</c>). Facultatif.</summary>
    public Action<SettingsState>? ApplyEffect { get; init; }

    /// <summary>Gain de performance estimé (overclocking).</summary>
    public decimal ExpectedGainPct { get; init; }

    /// <summary>Revert exact garanti (valeur d'origine). Reporté dans <c>OcProposal.step.revertExact</c>.</summary>
    public string RevertExact { get; init; } = "";
}

/// <summary>Paire de réglages à ne jamais appliquer ensemble (calque CDC §13.1).</summary>
public sealed record Incompatibility(string A, string B, string Reason)
{
    public bool IsPair(string x, string y) => (A == x && B == y) || (A == y && B == x);
}

/// <summary>Le catalogue consommé par le moteur de sélection (contenu = livrable humain).</summary>
public interface ICatalog
{
    IReadOnlyList<CatalogTweak> Tweaks { get; }
    IReadOnlyList<Incompatibility> Incompatibilities { get; }
}
