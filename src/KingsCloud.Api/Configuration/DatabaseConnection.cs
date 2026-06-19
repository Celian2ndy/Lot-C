namespace Kings.Cloud.Api.Configuration;

/// <summary>
/// Résout la chaîne de connexion PostgreSQL : d'abord <c>ConnectionStrings:Default</c> (config/env),
/// sinon construite depuis les variables <c>POSTGRES_*</c> (avec valeurs DEV LOCAL par défaut,
/// alignées sur docker-compose). En prod, la chaîne vient de la config/secrets (acte humain).
/// </summary>
public static class DatabaseConnection
{
    public static string Resolve(IConfiguration? config = null)
    {
        var fromConfig = config?.GetConnectionString("Default");
        if (!string.IsNullOrWhiteSpace(fromConfig)) return fromConfig;

        var host = Env("POSTGRES_HOST", "localhost");
        var port = Env("POSTGRES_PORT", "5432");
        var db = Env("POSTGRES_DB", "kingscloud");
        var user = Env("POSTGRES_USER", "kings");
        var pwd = Env("POSTGRES_PASSWORD", "kings_local_dev"); // DEV LOCAL uniquement
        return $"Host={host};Port={port};Database={db};Username={user};Password={pwd}";
    }

    private static string Env(string key, string fallback)
        => Environment.GetEnvironmentVariable(key) is { Length: > 0 } v ? v : fallback;
}
