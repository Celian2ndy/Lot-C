using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Kings.Score.Json;

/// <summary>
/// Convertisseur System.Text.Json qui respecte <see cref="EnumMemberAttribute"/> pour les enums.
/// Indispensable pour reproduire EXACTEMENT les valeurs string du contrat kings-schemas
/// (p. ex. "gpu", "NVMe", "OK", "Win10", "veryLow") — que le convertisseur natif de .NET 8 ignore.
/// Les types générés référencent cette fabrique via [JsonConverter(typeof(EnumMemberJsonConverterFactory))].
/// </summary>
public sealed class EnumMemberJsonConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert) => typeToConvert.IsEnum;

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        var converterType = typeof(EnumMemberJsonConverter<>).MakeGenericType(typeToConvert);
        return (JsonConverter)Activator.CreateInstance(converterType)!;
    }
}

/// <summary>Convertisseur typé honorant <see cref="EnumMemberAttribute"/> (valeur exacte du contrat).</summary>
public sealed class EnumMemberJsonConverter<TEnum> : JsonConverter<TEnum> where TEnum : struct, Enum
{
    private static readonly Dictionary<TEnum, string> ToJson = new();
    private static readonly Dictionary<string, TEnum> FromJson = new(StringComparer.Ordinal);

    static EnumMemberJsonConverter()
    {
        foreach (var field in typeof(TEnum).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            var value = (TEnum)field.GetValue(null)!;
            var name = field.GetCustomAttribute<EnumMemberAttribute>()?.Value ?? field.Name;
            ToJson[value] = name;
            FromJson[name] = value;
        }
    }

    public override TEnum Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var s = reader.GetString();
        if (s is not null && FromJson.TryGetValue(s, out var exact))
            return exact;

        // Tolérance défensive : correspondance insensible à la casse (déterministe : premier match ordinal).
        if (s is not null)
        {
            foreach (var kv in FromJson)
            {
                if (string.Equals(kv.Key, s, StringComparison.OrdinalIgnoreCase))
                    return kv.Value;
            }
        }

        throw new JsonException($"Valeur '{s}' invalide pour l'enum {typeof(TEnum).Name}.");
    }

    public override void Write(Utf8JsonWriter writer, TEnum value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(ToJson.TryGetValue(value, out var s) ? s : value.ToString());
    }
}
