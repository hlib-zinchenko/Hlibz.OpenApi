using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Atlas.Api.Domain;

/// <summary>
/// A country's id. Written as the GUID itself, so <c>AddStronglyTypedIds()</c> publishes it as
/// <c>{ "type": "string", "format": "uuid" }</c>; <see cref="IParsable{TSelf}"/> binds it from a
/// route or query string.
/// </summary>
[JsonConverter(typeof(CountryIdJsonConverter))]
public readonly record struct CountryId(Guid Value) : IParsable<CountryId>
{
    public static CountryId New() => new(Guid.CreateVersion7());

    public static CountryId Parse(string s, IFormatProvider? provider) =>
        new(Guid.Parse(s, provider));

    public static bool TryParse(
        [NotNullWhen(true)] string? s,
        IFormatProvider? provider,
        out CountryId result)
    {
        bool parsed = Guid.TryParse(s, provider, out Guid value);
        result = new CountryId(value);
        return parsed;
    }

    public override string ToString() => Value.ToString();
}

internal sealed class CountryIdJsonConverter : JsonConverter<CountryId>
{
    public override CountryId Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options) =>
        new(reader.GetGuid());

    public override void Write(
        Utf8JsonWriter writer,
        CountryId value,
        JsonSerializerOptions options) =>
        writer.WriteStringValue(value.Value);
}
