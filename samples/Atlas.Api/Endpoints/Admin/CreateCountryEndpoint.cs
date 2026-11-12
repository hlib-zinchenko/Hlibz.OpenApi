using Atlas.Api.Domain;

using Hlibz.MinimalApi.Endpoints;

using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.Api.Endpoints.Admin;

/// <summary>
/// Adds a country to the atlas and returns it, with its new id.
/// </summary>
/// <remarks>
/// The request shows <c>[CountryCode]</c> (a <c>pattern</c>), <c>[DistinctItems]</c>
/// (<c>uniqueItems</c>), and an <c>[OpenApiRule]</c> on an enum property: the enum is a shared
/// component, so the sentence goes next to its <c>$ref</c>.
/// </remarks>
internal sealed class CreateCountryEndpoint : IEndpoint<AdminAtlasEndpointGroup, CountryDto>
{
    public RouteHandlerBuilder Map(IEndpointRouteBuilder group) =>
        group.MapPost("countries", Handle)
            .WithSummary("Adds a country.");

    private static Created<CountryDto> Handle(
        CreateCountryRequest request,
        [FromServices] AtlasStore store)
    {
        CountryDto country = store.AddCountry(request.Code, request.Name, request.Continent);
        return TypedResults.Created($"/api/countries/{country.Id}", country);
    }
}
