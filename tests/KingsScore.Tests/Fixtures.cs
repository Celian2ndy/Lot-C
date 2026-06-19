using System.Text.Json;
using System.Text.Json.Nodes;
using Kings.Score.Contracts.Snapshot;
using Kings.Score.Json;

namespace KingsScore.Tests;

/// <summary>
/// Charge les fixtures partagées du sous-module kings-schemas (source de vérité v1.0.1).
/// Le Lot C se teste DIRECTEMENT contre ces fixtures (entrée + score attendu), sans aucun client.
/// </summary>
public sealed record Fixture(string Name, SystemSnapshot Snapshot, JsonNode Root);

public static class Fixtures
{
    /// <summary>Options STJ honorant les valeurs string EXACTES du contrat (via EnumMember).</summary>
    public static readonly JsonSerializerOptions Json = new()
    {
        Converters = { new EnumMemberJsonConverterFactory() },
    };

    private static readonly Lazy<string> FixturesDir = new(LocateFixturesDir);

    public static Fixture Load(string name)
    {
        var path = Path.Combine(FixturesDir.Value, name + ".json");
        var text = File.ReadAllText(path);
        var root = JsonNode.Parse(text) ?? throw new InvalidOperationException($"Fixture vide : {path}");

        var input = root["input"] ?? throw new InvalidOperationException($"Fixture sans 'input' : {path}");
        var snapshot = input.Deserialize<SystemSnapshot>(Json)
                       ?? throw new InvalidOperationException($"Désérialisation impossible : {path}");

        return new Fixture(name, snapshot, root);
    }

    private static string LocateFixturesDir()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null)
        {
            var candidate = Path.Combine(dir, "schemas", "fixtures");
            if (Directory.Exists(candidate))
                return candidate;
            dir = Directory.GetParent(dir)?.FullName;
        }
        throw new DirectoryNotFoundException(
            "Dossier schemas/fixtures introuvable en remontant depuis " + AppContext.BaseDirectory);
    }
}
