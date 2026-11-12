using Atlas.Api.Domain;

using Hlibz.MinimalApi.Endpoints;

using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.Api.Endpoints.Countries;

/// <summary>
/// Gets one country by its id; an unknown id is a <c>404</c>.
/// </summary>
/// <remarks>
/// The <c>{id}</c> route parameter is a <see cref="CountryId"/>, bound through its
/// <c>TryParse</c>: <c>AddStronglyTypedIds()</c> documents it as a <c>uuid</c> string, not the
/// bare <c>string</c> ASP.NET Core would. The summary starts with "Gets", and
/// <c>AddDescriptionCleanup()</c> leaves it: it only cleans schemas and parameters.
/// </remarks>
internal sealed class GetCountryEndpoint : IEndpoint<CountriesEndpointGroup, CountryDto>
{
    public RouteHandlerBuilder Map(IEndpointRouteBuilder group) =>
        group.MapGet("{id}", Handle)
            .WithSummary("Gets one country by its id.")
            .ProducesErrors(StatusCodes.Status404NotFound);

    private static Results<Ok<CountryDto>, NotFound> Handle(
        CountryId id,
        [FromServices] AtlasStore store) =>
        store.FindCountry(id) is { } country ? TypedResults.Ok(country) : TypedResults.NotFound();
}
