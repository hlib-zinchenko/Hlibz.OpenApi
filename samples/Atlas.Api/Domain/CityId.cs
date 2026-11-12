using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Atlas.Api.Domain;

/// <summary>
/// A city's id: an <see cref="int"/>, to show an ID taking the schema of whatever it wraps
/// (<c>{ "type": "integer", "format": "int32" }</c>), not only GUIDs.
/// </summary>
[JsonConverter(typeof(CityIdJsonConverter))]
public readonly record struct CityId(int Value) : IParsable<CityId>
{
    public static CityId Parse(string s, IFormatProvider? provider) =>
        new(int.Parse(s, NumberStyles.Integer, provider));

    public static bool TryParse(
        [NotNullWhen(true)] string? s,
        IFormatProvider? provider,
        out CityId result)
    {
        bool parsed = int.TryParse(s, NumberStyles.Integer, provider, out int value);
        result = new CityId(value);
        return parsed;
    }

    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}

internal sealed class CityIdJsonConverter : JsonConverter<CityId>
{
    public override CityId Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options) =>
        new(reader.GetInt32());

    public override void Write(
        Utf8JsonWriter writer,
        CityId value,
        JsonSerializerOptions options) =>
        writer.WriteNumberValue(value.Value);
}
