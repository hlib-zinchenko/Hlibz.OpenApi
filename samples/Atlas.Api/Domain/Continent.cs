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
    /// <summary>Africa.</summary>
    Africa,

    /// <summary>Antarctica, which no country lies on.</summary>
    Antarctica,

    /// <summary>Asia, including the Middle East.</summary>
    Asia,

    /// <summary>Europe.</summary>
    Europe,

    /// <summary>North America, including Central America and the Caribbean.</summary>
    NorthAmerica,

    /// <summary>Australia, New Zealand and the Pacific islands.</summary>
    Oceania,

    /// <summary>South America.</summary>
    SouthAmerica,
}
