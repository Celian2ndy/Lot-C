using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace KingsCloud.Api.Tests;

/// <summary>
/// Démarre l'API en mémoire (WebApplicationFactory) en environnement Development : applique les
/// migrations + le seed (compte/licence de test) sur la vraie base PostgreSQL (docker-compose).
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder) => builder.UseEnvironment("Development");
}

/// <summary>Charge le SystemSnapshot d'une fixture partagée (pour servir de rawMetrics).</summary>
public static class ApiFixtures
{
    public static JsonNode HighEndInput()
    {
        var dir = LocateFixturesDir();
        var root = JsonNode.Parse(File.ReadAllText(Path.Combine(dir, "fixture_nvidia_intel_highend.json")))!;
        return root["input"]!.DeepClone();
    }

    private static string LocateFixturesDir()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null)
        {
            var candidate = Path.Combine(dir, "schemas", "fixtures");
            if (Directory.Exists(candidate)) return candidate;
            dir = Directory.GetParent(dir)?.FullName;
        }
        throw new DirectoryNotFoundException("schemas/fixtures introuvable depuis " + AppContext.BaseDirectory);
    }
}
