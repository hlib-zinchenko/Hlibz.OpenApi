using Atlas.Api.Domain;

using Hlibz.Results;
using Hlibz.Results.AspNetCore;

using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.Api.Endpoints;

/// <summary>
/// The admin API, <c>api/admin</c>, in its own document: every endpoint requires the admin
/// policy, unless it opts out. Documented as <see cref="CountryEndpoints"/> are.
/// </summary>
internal static class AdminEndpoints
{
    public static void MapAdmin(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder admin = app.MapGroup("api/admin")
            .WithTags("Atlas admin")
            .WithGroupName(Documents.AdminGroup)
            .RequireAuthorization(Policies.Admin)
            .WithResultResponses();

        admin.MapPost("countries", CreateCountry).WithName(nameof(CreateCountry));
        admin.MapDelete("countries/{id}", DeleteCountry).WithName(nameof(DeleteCountry));
        admin.MapPost("cities", CreateCity)
            .WithName(nameof(CreateCity))
            .WithCreatedLocation<CityDto>(city => $"/api/cities/{city.Id}");
        admin.MapGet("status", GetStatus)
            .WithName(nameof(GetStatus))
            .AllowAnonymous();
    }

    // The request shows [CountryCode] (a pattern), [DistinctItems] (uniqueItems), and an
    // [OpenApiRule] on an enum property: the enum is a shared component, so the sentence goes next
    // to its $ref.

    /// <summary>Adds a country.</summary>
    /// <remarks>Adds a country to the atlas and returns it, with its new id.</remarks>
    internal static Created<CountryDto> CreateCountry(
        CreateCountryRequest request,
        [FromServices] AtlasStore store)
    {
        CountryDto country = store.AddCountry(request.Code, request.Name, request.Continent);
        return TypedResults.Created($"/api/countries/{country.Id}", country);
    }

    // Its 409 is CountryHasCities' own body: the ids of the cities in the way, inlined as the int32s
    // a CityId stands for.

    /// <summary>Deletes a country.</summary>
    /// <remarks>Deletes a country that has no cities left: delete its cities first.</remarks>
    internal static Result<Unit, DeleteCountryError> DeleteCountry(
        CountryId id,
        [FromServices] AtlasStore store)
    {
        if (store.FindCountry(id) is null)
        {
            return new CountryNotFound(id);
        }

        IReadOnlyList<CityDto> cities = store.CitiesOf(id);
        if (cities.Count > 0)
        {
            return new CountryHasCities([.. cities.Select(city => city.Id)]);
        }

        store.RemoveCountry(id);
        return Unit.Value;
    }

    // Its 409 is one of two errors, CityNameTaken or CapitalTaken: a oneOf told apart by code.

    /// <summary>Adds a city.</summary>
    /// <remarks>Adds a city to an existing country. A country has one capital, and one city by each name.</remarks>
    internal static Result<CityDto, CreateCityError> CreateCity(
        CreateCityRequest request,
        [FromServices] AtlasStore store)
    {
        if (store.FindCountry(request.CountryId) is null)
        {
            return new CountryNotFound(request.CountryId);
        }

        IReadOnlyList<CityDto> cities = store.CitiesOf(request.CountryId);
        if (cities.FirstOrDefault(city =>
                string.Equals(city.Name, request.Name, StringComparison.OrdinalIgnoreCase)) is { } namesake)
        {
            return new CityNameTaken(namesake.Id);
        }

        if (request.IsCapital && cities.FirstOrDefault(city => city.IsCapital) is { } capital)
        {
            return new CapitalTaken(capital.Id);
        }

        return store.AddCity(request.CountryId, request.Name, request.Population, request.IsCapital);
    }

    // Its group requires the admin policy, but AllowAnonymous() opens it again, so the docs show no
    // lock on it.

    /// <summary>Gets the admin API's status.</summary>
    /// <remarks>Gets how many countries, cities and pending suggestions the atlas holds. Open to everyone, as a health check.</remarks>
    internal static Ok<AdminStatusDto> GetStatus([FromServices] AtlasStore store) =>
        TypedResults.Ok(new AdminStatusDto(
            store.Countries.Count,
            store.Cities.Count,
            store.PendingSuggestions));
}
