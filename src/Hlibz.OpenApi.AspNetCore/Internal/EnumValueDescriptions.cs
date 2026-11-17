using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;

namespace Hlibz.OpenApi.Internal;

/// <summary>
/// The XML <c>&lt;summary&gt;</c> of each value of an enum, for the <see cref="ExtensionName"/>
/// extension, keyed the way the schema lists the values: as JSON writes them (a name after
/// <c>JsonStringEnumConverter</c>'s naming policy and <c>[JsonStringEnumMemberName]</c>, or a
/// number), or by their declared names for a route, query or header parameter, which binds with
/// <c>Enum.TryParse</c>.
/// </summary>
/// <remarks>
/// Every value is listed, with an empty description when it has no summary: ReDoc shows
/// <see cref="ExtensionName"/> in place of <c>enum</c>, so a value left out would vanish from the docs.
/// </remarks>
internal static class EnumValueDescriptions
{
    /// <summary>The extension ReDoc (and Scalar) read: each value → its description.</summary>
    public const string ExtensionName = "x-enumDescriptions";

    private static readonly ConcurrentDictionary<(Type Type, JsonSerializerOptions Options), EnumValue[]?>
        Values = new();

    /// <summary>
    /// Gets the extension for the enum <paramref name="type"/>, when at least one of its values has
    /// a summary and the schema's <c>enum</c>, if it has one, lists the values in a form it knows.
    /// </summary>
    /// <param name="type">The enum.</param>
    /// <param name="options">The JSON options the schema was generated with.</param>
    /// <param name="schema">The enum's schema.</param>
    /// <returns>The extension, or <see langword="null"/>.</returns>
    public static JsonObject? Read(Type type, JsonSerializerOptions options, OpenApiSchema schema)
    {
        if (Values.GetOrAdd((type, options), Load) is not { } values
            || Key(schema, values) is not { } key)
        {
            return null;
        }

        JsonObject extension = [];
        foreach (EnumValue value in values)
        {
            // An alias lists as the value it repeats, or as its own name: the first summary wins.
            string name = key(value);
            if (!extension.TryGetPropertyValue(name, out JsonNode? existing)
                || (existing?.GetValue<string>().Length == 0 && value.Summary.Length > 0))
            {
                extension[name] = value.Summary;
            }
        }

        return extension;
    }

    private static EnumValue[]? Load((Type Type, JsonSerializerOptions Options) key)
    {
        JsonTypeInfo typeInfo = key.Options.GetTypeInfo(key.Type);
        List<EnumValue> values = [];
        foreach (string name in Enum.GetNames(key.Type))
        {
            values.Add(new EnumValue(
                name,
                JsonForm(Enum.Parse(key.Type, name), typeInfo),
                XmlDocumentation.EnumValueSummary(key.Type, name) ?? string.Empty));
        }

        return values.Exists(value => value.Summary.Length > 0) ? [.. values] : null;
    }

    /// <summary>A value as JSON writes it, or <see langword="null"/> when a converter writes neither a string nor a number.</summary>
    private static string? JsonForm(object value, JsonTypeInfo typeInfo)
    {
        JsonElement json = JsonSerializer.SerializeToElement(value, typeInfo);
        return json.ValueKind switch
        {
            JsonValueKind.String => json.GetString(),
            JsonValueKind.Number => json.GetRawText(),
            _ => null,
        };
    }

    /// <summary>
    /// How the schema lists the values: as JSON writes them (also when it has no <c>enum</c>, as for
    /// an integer enum), or by declared name; <see langword="null"/> when neither matches, such as
    /// under a property whose own converter writes the enum differently.
    /// </summary>
    private static Func<EnumValue, string>? Key(OpenApiSchema schema, EnumValue[] values)
    {
        if (schema.Enum is not { Count: > 0 } listed)
        {
            return values.All(value => value.Json is not null) ? value => value.Json! : null;
        }

        HashSet<string> names = new(
            listed.Select(node => node is JsonValue value && value.TryGetValue(out string? text) ? text : node?.ToJsonString())
                .OfType<string>(),
            StringComparer.Ordinal);
        if (values.All(value => value.Json is not null) && names.SetEquals(values.Select(value => value.Json!)))
        {
            return value => value.Json!;
        }

        return names.SetEquals(values.Select(value => value.Name)) ? value => value.Name : null;
    }

    /// <summary>An enum value.</summary>
    /// <param name="Name">Its declared name.</param>
    /// <param name="Json">Its JSON form, if a string or a number.</param>
    /// <param name="Summary">Its summary; empty when it has none.</param>
    private sealed record EnumValue(string Name, string? Json, string Summary);
}
