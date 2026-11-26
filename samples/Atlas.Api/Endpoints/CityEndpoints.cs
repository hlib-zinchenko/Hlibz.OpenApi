using Atlas.Api.Domain;

using Hlibz.Results;
using Hlibz.Results.AspNetCore;

using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.Api.Endpoints;

/// <summary>The public <c>api/cities</c> endpoints, documented as <see cref="CountryEndpoints"/> are.</summary>
internal static class CityEndpoints
{
    public static void MapCities(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder cities = app.MapGroup("api/cities")
            .WithTags("Cities")
            .WithGroupName(Documents.PublicGroup)
            .WithResultResponses();

        cities.MapGet(string.Empty, SearchCities).WithName(nameof(SearchCities));
        cities.MapGet("{id}", GetCity).WithName(nameof(GetCity));
        cities.MapPost("suggestions", SuggestCity)
            .WithName(nameof(SuggestCity))
            .RequireAuthorization(Policies.Member);
    }

    // The filters are an [AsParameters] query: each property is a query parameter, described by its
    // XML summary (cleaned of "Gets or sets"), its rule attributes (uniqueItems on codes and
    // countryIds, not on their items) and, for the nullable CountryId and the CountryId array, the
    // uuid schema of a strongly-typed ID. The type's own [OpenApiRule] is appended to the description.

    /// <summary>Gets the cities matching every filter.</summary>
    /// <remarks>Gets the cities matching every filter given, in the order they were added.</remarks>
    internal static Ok<IReadOnlyList<CityDto>> SearchCities(
        [AsParameters] SearchCitiesQuery query,
        [FromServices] AtlasStore store)
    {
        HashSet<CountryId>? countries = query.Codes is null
            ? null
            : store.Countries
                .Where(country => query.Codes.Contains(country.Code, StringComparer.Ordinal))
                .Select(country => country.Id)
                .ToHashSet();

        IReadOnlyList<CityDto> cities =
        [
            .. store.Cities
                .Where(city => query.CountryId is null || city.CountryId == query.CountryId)
                .Where(city => query.CountryIds is null || query.CountryIds.Contains(city.CountryId))
                .Where(city => countries is null || countries.Contains(city.CountryId))
                .Where(city => query.MinPopulation is null || city.Population >= query.MinPopulation)
                .Where(city => query.MaxPopulation is null || city.Population <= query.MaxPopulation),
        ];
        return TypedResults.Ok(cities);
    }

    // A CityId wraps an int, so it is documented as one.

    /// <summary>Gets one city by its id.</summary>
    /// <remarks>An unknown id is a `404`.</remarks>
    internal static Result<CityDto, GetCityError> GetCity(CityId id, [FromServices] AtlasStore store) =>
        store.FindCity(id) is { } city ? city : new CityNotFound(id);

    // The Member policy puts a lock on this endpoint, and only this one, in the public document.
    // countryId carries [NotDefault]: a not keyword and a sentence on the inlined ID.

    /// <summary>Suggests a missing city.</summary>
    /// <remarks>Suggests a city the atlas is missing, for an admin to review. Needs a signed-in user.</remarks>
    internal static Accepted SuggestCity(SuggestCityRequest request, [FromServices] AtlasStore store)
    {
        store.Suggest(request);
        return TypedResults.Accepted((string?)null);
    }
}
