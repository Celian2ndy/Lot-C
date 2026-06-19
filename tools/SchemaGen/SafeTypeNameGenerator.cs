using NJsonSchema;

namespace SchemaGen;

// Évite qu'un objet imbriqué soit nommé d'après un identifiant réservé qui casserait la
// compilation (p. ex. settingsState.system -> classe "System" qui masque le namespace global System).
internal sealed class SafeTypeNameGenerator : DefaultTypeNameGenerator
{
    private static readonly HashSet<string> Reserved = new(StringComparer.Ordinal)
    {
        "System", "Object", "String", "Math", "Console", "Type", "Enum", "Array",
        "Task", "Guid", "DateTime", "DateTimeOffset", "Action", "Func", "Tuple",
    };

    public override string Generate(JsonSchema schema, string? typeNameHint, IEnumerable<string> reservedTypeNames)
    {
        var name = base.Generate(schema, typeNameHint, reservedTypeNames);
        return Reserved.Contains(name) ? name + "Settings" : name;
    }
}
