using Hlibz.Results;

namespace Atlas.Api.Domain;

/// <summary>No country has the id asked for.</summary>
/// <param name="CountryId">Gets the id that wasn't found.</param>
[Error("atlas_1001", ResultStatus.NotFound, "The country wasn't found.")]
public sealed record CountryNotFound(CountryId CountryId) : Error;

/// <summary>No city has the id asked for.</summary>
/// <param name="CityId">Gets the id that wasn't found.</param>
[Error("atlas_1002", ResultStatus.NotFound, "The city wasn't found.")]
public sealed record CityNotFound(CityId CityId) : Error;

/// <summary>The country already has a city by that name.</summary>
/// <param name="CityId">Gets the id of the city that has the name.</param>
[Error("atlas_1003", ResultStatus.Conflict, "The country already has a city by that name.")]
public sealed record CityNameTaken(CityId CityId) : Error;

/// <summary>The country already has a capital, and a country has one.</summary>
/// <param name="CapitalId">Gets the id of the country's capital.</param>
[Error("atlas_1004", ResultStatus.Conflict, "The country already has a capital.")]
public sealed record CapitalTaken(CityId CapitalId) : Error;

/// <summary>A capital is added without its population.</summary>
[Error("atlas_1006", ResultStatus.Error, "A capital needs its population.")]
public sealed record CapitalWithoutPopulation : Error;

/// <summary>The country still has cities: delete them first.</summary>
/// <param name="CityIds">Gets the ids of the country's cities.</param>
[Error("atlas_1005", ResultStatus.Conflict, "The country still has cities.")]
public sealed record CountryHasCities(IReadOnlyList<CityId> CityIds) : Error;
