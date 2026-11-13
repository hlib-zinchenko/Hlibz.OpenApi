using Atlas.Api.Domain;

using Hlibz.MinimalApi.Endpoints;

using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.Api.Endpoints.Cities;

/// <summary>
/// Gets the cities matching every filter given, in the order they were added.
/// </summary>
/// <remarks>
/// The filters are an <c>[AsParameters]</c> query: each property is a query parameter, described
/// by its XML summary (cleaned of "Gets or sets"), its rule attributes (<c>uniqueItems</c> on
/// <c>codes</c> and <c>countryIds</c>, not on their items) and, for the nullable
/// <see cref="CountryId"/> and the <see cref="CountryId"/> array, the <c>uuid</c> schema of a
/// strongly-typed ID. The type's own <c>[OpenApiRule]</c> is appended to this description.
/// </remarks>
internal sealed class SearchCitiesEndpoint : IEndpoint<CitiesEndpointGroup>
{
    public RouteHandlerBuilder Map(IEndpointRouteBuilder group) =>
        group.MapGet(string.Empty, Handle).WithSummary("Gets the cities matching every filter.");

    private static Ok<IReadOnlyList<CityDto>> Handle(
        [AsParameters] SearchCitiesQuery query,
        [FromServices] AtlasStore store)
    {
        HashSet<CountryId>? countries = query.Codes is null
            ? null
            : [.. store.Countries
                .Where(country => query.Codes.Contains(country.Code, StringComparer.Ordinal))
                .Select(country => country.Id)];

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
}
