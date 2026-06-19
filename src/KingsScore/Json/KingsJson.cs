using System.Text.Json;

namespace Kings.Score.Json;

/// <summary>Options System.Text.Json partagées (valeurs string EXACTES du contrat via EnumMember).</summary>
public static class KingsJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        Converters = { new EnumMemberJsonConverterFactory() },
    };
}
