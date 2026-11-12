using System.Text.Json.Serialization;

namespace Atlas.Api.Domain;

/// <summary>A continent.</summary>
/// <remarks>
/// Published as a component schema of its own, shared by every use: a rule on a property of this
/// type goes next to the property's <c>$ref</c>, never onto the shared component.
/// </remarks>
[JsonConverter(typeof(JsonStringEnumConverter<Continent>))]
public enum Continent
{
    Africa,
    Antarctica,
    Asia,
    Europe,
    NorthAmerica,
    Oceania,
    SouthAmerica,
}
