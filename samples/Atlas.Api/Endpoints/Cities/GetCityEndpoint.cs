using Atlas.Api.Domain;

using Hlibz.MinimalApi.Endpoints;

using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.Api.Endpoints.Cities;

/// <summary>
/// Gets one city by its id; an unknown id is a <c>404</c>.
/// </summary>
/// <remarks>A <see cref="CityId"/> wraps an <see cref="int"/>, so it is documented as one.</remarks>
internal sealed class GetCityEndpoint : IEndpoint<CitiesEndpointGroup>
{
    public RouteHandlerBuilder Map(IEndpointRouteBuilder group) =>
        group.MapGet("{id}", Handle)
            .WithSummary("Gets one city by its id.")
            .ProducesErrors(StatusCodes.Status404NotFound);

    private static Results<Ok<CityDto>, NotFound> Handle(
        CityId id,
        [FromServices] AtlasStore store) =>
        store.FindCity(id) is { } city ? TypedResults.Ok(city) : TypedResults.NotFound();
}
