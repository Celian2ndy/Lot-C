using NJsonSchema;
using NJsonSchema.CodeGeneration.CSharp;
using SchemaGen;

// Génère les types C# des objets pivots depuis les JSON Schema (kings-schemas v1.0.1).
// Usage : dotnet run --project tools/SchemaGen -- <schemaDir> <outDir>
//   schemaDir = schemas/json-schema/objects
//   outDir    = src/KingsScore/Contracts/Generated
//
// Chaque objet est généré dans un sous-namespace distinct pour éviter toute collision de
// noms entre objets (p. ex. l'enum Domain présent dans plusieurs schémas).

if (args.Length < 2)
{
    Console.Error.WriteLine("Usage: SchemaGen <schemaDir> <outDir>");
    return 1;
}

var schemaDir = args[0];
var outDir = args[1];

// Mapping fichier d'objet -> sous-namespace court (sans collision namespace/classe).
var nsByObject = new Dictionary<string, string>(StringComparer.Ordinal)
{
    ["SystemSnapshot"] = "Snapshot",
    ["ScoreResult"] = "Score",
    ["OptimizationPlan"] = "Plan",
    ["OptimizationReport"] = "Report",
    ["OcProposal"] = "Oc",
    ["Alert"] = "Alerts",
};

Directory.CreateDirectory(outDir);

// Nettoyage : on régénère intégralement le dossier.
foreach (var old in Directory.GetFiles(outDir, "*.g.cs"))
    File.Delete(old);

var files = Directory.GetFiles(schemaDir, "*.schema.json").OrderBy(f => f, StringComparer.Ordinal).ToList();
if (files.Count == 0)
{
    Console.Error.WriteLine($"Aucun schéma trouvé dans {schemaDir}");
    return 1;
}

foreach (var file in files)
{
    var objectName = Path.GetFileName(file).Replace(".schema.json", "", StringComparison.Ordinal);
    var subNs = nsByObject.TryGetValue(objectName, out var ns) ? ns : objectName;

    var schema = await JsonSchema.FromFileAsync(file);

    var settings = new CSharpGeneratorSettings
    {
        Namespace = $"Kings.Score.Contracts.{subNs}",
        JsonLibrary = CSharpJsonLibrary.SystemTextJson,
        ClassStyle = CSharpClassStyle.Poco,
        GenerateNullableReferenceTypes = true,
        GenerateOptionalPropertiesAsNullable = true,
        GenerateDataAnnotations = false,
        ArrayType = "System.Collections.Generic.IReadOnlyList",
        ArrayInstanceType = "System.Collections.Generic.List",
        GenerateDefaultValues = false,
        TypeNameGenerator = new SafeTypeNameGenerator(),
    };

    var generator = new CSharpGenerator(schema, settings);
    var code = generator.GenerateFile();

    // Les enums du contrat doivent reproduire leurs valeurs string EXACTES via [EnumMember].
    // Le convertisseur natif de .NET 8 les ignore : on branche notre fabrique EnumMember.
    code = code.Replace(
        "typeof(System.Text.Json.Serialization.JsonStringEnumConverter)",
        "typeof(Kings.Score.Json.EnumMemberJsonConverterFactory)",
        StringComparison.Ordinal);

    var outPath = Path.Combine(outDir, $"{objectName}.g.cs");
    File.WriteAllText(outPath, code);
    Console.WriteLine($"Généré : {objectName} -> {settings.Namespace} ({outPath})");
}

return 0;
