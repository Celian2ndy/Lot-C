using System.Text.Json;
using Kings.Cloud.Api.Security;
using Kings.Score.Contracts.Snapshot;
using Kings.Score.Json;
using Kings.Score.Scoring;

namespace Kings.Cloud.Api.Services;

public sealed record RecomputeResult(int Score, string WeightsetVersion, string Tier, string ConfigHash, Guid SnapshotId);

/// <summary>
/// 🛡️ ANTI-TRICHE (C3) : recalcule le score du leaderboard côté serveur à partir des métriques BRUTES,
/// avec le MÊME barème versionné que le client (bibliothèque KingsScore). Aucune valeur de score envoyée
/// par le client n'est lue — il n'y a d'ailleurs pas de champ « score » dans le contrat de soumission.
/// </summary>
public sealed class LeaderboardScoring
{
    private readonly ScoreEngine _engine = new();

    public RecomputeResult Recompute(JsonElement rawMetrics)
    {
        SystemSnapshot snapshot;
        try
        {
            snapshot = rawMetrics.Deserialize<SystemSnapshot>(KingsJson.Options)
                       ?? throw new InvalidDataException("rawMetrics vide.");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("rawMetrics invalide : " + ex.Message, ex);
        }

        // scoreId/computedAt sont des métadonnées injectées (sans effet sur le score) — voir C1.
        var result = _engine.Compute(snapshot, Guid.NewGuid(), DateTimeOffset.UtcNow);

        return new RecomputeResult(
            result.Global,
            result.WeightsetVersion,
            TierClassifier.Classify(snapshot),
            ConfigHash.Compute(snapshot),
            snapshot.SnapshotId);
    }
}

/// <summary>
/// PROVISOIRE — heuristique de tier (budget/mid/highEnd) à valider. Le vrai tier viendra d'un mapping
/// de configuration (cloud/catalogue). Ne sert qu'au regroupement du classement.
/// </summary>
public static class TierClassifier
{
    public static string Classify(SystemSnapshot s)
    {
        var renderVram = s.Hardware.Gpus.Where(g => g.IsRenderGpu).Select(g => g.VramMB).DefaultIfEmpty(0).Max();
        var vram = renderVram > 0 ? renderVram : s.Hardware.Gpus.Select(g => g.VramMB).DefaultIfEmpty(0).Max();
        if (vram >= 12288) return "highEnd";
        if (vram >= 8192) return "mid";
        return "budget";
    }
}

/// <summary>Empreinte ANONYME de la configuration (sert au tier et à l'agrégation, sans donnée personnelle).</summary>
public static class ConfigHash
{
    public static string Compute(SystemSnapshot s)
    {
        var g = s.Hardware.Gpus.FirstOrDefault(x => x.IsRenderGpu) ?? s.Hardware.Gpus.FirstOrDefault();
        var canonical = string.Join("|",
            s.Hardware.Cpu.Vendor, s.Hardware.Cpu.Model,
            g?.Vendor.ToString() ?? "?", g?.Model ?? "?",
            s.Hardware.Ram.TotalMB);
        return Hashing.Sha256Hex(canonical);
    }
}
