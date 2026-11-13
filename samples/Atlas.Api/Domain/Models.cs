using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

using Atlas.Api.Rules;

using Hlibz.OpenApi;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Validation;

namespace Atlas.Api.Domain;

/// <summary>A country, as listed to everyone.</summary>
/// <param name="Id">Gets the country's id.</param>
/// <param name="Code">Gets the country's ISO 3166-1 alpha-2 code.</param>
/// <param name="Name">Gets the country's English name.</param>
/// <param name="Continent">Gets the continent the country lies on.</param>
public sealed record CountryDto(CountryId Id, string Code, string Name, Continent Continent);

/// <summary>A city.</summary>
/// <param name="Id">Gets the city's id.</param>
/// <param name="CountryId">Gets the id of the country the city is in.</param>
/// <param name="Name">Gets the city's English name.</param>
/// <param name="Population">Gets the city's population, if known.</param>
/// <param name="IsCapital">Gets a value indicating whether the city is its country's capital.</param>
public sealed record CityDto(
    CityId Id,
    CountryId CountryId,
    string Name,
    int? Population,
    bool IsCapital);

/// <summary>Filters the city list. Every filter is optional.</summary>
[ValidatableType]
[OpenApiRule("`minPopulation` must not be greater than `maxPopulation`.")]
public sealed class SearchCitiesQuery : IValidatableObject
{
    /// <summary>Gets or sets the country to list the cities of.</summary>
    [FromQuery(Name = "countryId")]
    public CountryId? CountryId { get; set; }

    /// <summary>Gets or sets the countries to list the cities of, instead of one country.</summary>
    [FromQuery(Name = "countryIds")]
    [DistinctItems]
    [SuppressMessage("Performance", "CA1819", Justification = "Minimal APIs bind a repeated query parameter to an array.")]
    public CountryId[]? CountryIds { get; set; }

    /// <summary>Gets or sets the smallest population to include.</summary>
    [FromQuery(Name = "minPopulation")]
    [Range(0, int.MaxValue)]
    public int? MinPopulation { get; set; }

    /// <summary>Gets or sets the largest population to include.</summary>
    [FromQuery(Name = "maxPopulation")]
    [Range(0, int.MaxValue)]
    public int? MaxPopulation { get; set; }

    /// <summary>Gets or sets the country codes to list the cities of, instead of one country.</summary>
    [FromQuery(Name = "codes")]
    [DistinctItems]
    [SuppressMessage("Performance", "CA1819", Justification = "Minimal APIs bind a repeated query parameter to an array.")]
    public string[]? Codes { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (MinPopulation > MaxPopulation)
        {
            yield return new ValidationResult(
                "minPopulation must not be greater than maxPopulation.",
                [nameof(MinPopulation), nameof(MaxPopulation)]);
        }
    }
}

/// <summary>Adds a country to the atlas.</summary>
[ValidatableType]
public sealed class CreateCountryRequest
{
    /// <summary>Gets or sets the country's ISO 3166-1 alpha-2 code.</summary>
    [Required]
    [CountryCode]
    public string Code { get; set; } = string.Empty;

    /// <summary>Gets or sets the country's English name.</summary>
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the continent the country lies on.</summary>
    [OpenApiRule("Antarctica has no countries.")]
    public Continent Continent { get; set; }

    /// <summary>Gets or sets the codes of the countries it borders.</summary>
    [DistinctItems]
    public IReadOnlyList<string> Borders { get; set; } = [];
}

/// <summary>Adds a city to a country.</summary>
[ValidatableType]
public sealed class CreateCityRequest
{
    /// <summary>Gets or sets the country the city is in.</summary>
    [NotDefault]
    public CountryId CountryId { get; set; }

    /// <summary>Gets or sets the city's English name.</summary>
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the city's population, if known.</summary>
    [Range(1, int.MaxValue)]
    public int? Population { get; set; }

    /// <summary>Gets or sets a value indicating whether the city is its country's capital.</summary>
    public bool IsCapital { get; set; }
}

/// <summary>Whether the admin API is up. Open to everyone, unlike the rest of the admin API.</summary>
/// <param name="Countries">Gets the number of countries in the atlas.</param>
/// <param name="Cities">Gets the number of cities in the atlas.</param>
/// <param name="PendingSuggestions">Gets the number of city suggestions waiting for review.</param>
public sealed record AdminStatusDto(int Countries, int Cities, int PendingSuggestions);

/// <summary>Suggests a city the atlas is missing, for an admin to review.</summary>
[ValidatableType]
internal sealed class SuggestCityRequest
{
    /// <summary>Gets or sets the country the city is in.</summary>
    [NotDefault]
    public CountryId CountryId { get; set; }

    /// <summary>Gets or sets the city's English name.</summary>
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets where the suggestion comes from, such as a link.</summary>
    [MaxLength(500)]
    public string? Source { get; set; }
}
