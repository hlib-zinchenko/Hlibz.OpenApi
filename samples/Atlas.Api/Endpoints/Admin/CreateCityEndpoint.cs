using Atlas.Api.Domain;

using Hlibz.MinimalApi.Endpoints;

using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.Api.Endpoints.Admin;

/// <summary>
/// Adds a city to an existing country; an unknown country is a <c>404</c>.
/// </summary>
internal sealed class CreateCityEndpoint : IEndpoint<AdminAtlasEndpointGroup>
{
    public RouteHandlerBuilder Map(IEndpointRouteBuilder group) =>
        group.MapPost("cities", Handle)
            .WithSummary("Adds a city.")
            .ProducesErrors(StatusCodes.Status404NotFound);

    private static Results<Created<CityDto>, NotFound> Handle(
        CreateCityRequest request,
        [FromServices] AtlasStore store)
    {
        if (store.FindCountry(request.CountryId) is null)
        {
            return TypedResults.NotFound();
        }

        CityDto city = store.AddCity(
            request.CountryId,
            request.Name,
            request.Population,
            request.IsCapital);
        return TypedResults.Created($"/api/cities/{city.Id}", city);
    }
}
