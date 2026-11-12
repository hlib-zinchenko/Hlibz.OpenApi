using Atlas.Api.Domain;

using Hlibz.MinimalApi.Endpoints;

using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.Api.Endpoints.Countries;

/// <summary>
/// Gets every country in the atlas, sorted by name.
/// </summary>
/// <remarks>Anonymous: no lock in the docs.</remarks>
internal sealed class GetCountriesEndpoint
    : IEndpoint<CountriesEndpointGroup, IReadOnlyList<CountryDto>>
{
    public RouteHandlerBuilder Map(IEndpointRouteBuilder group) =>
        group.MapGet(string.Empty, Handle).WithSummary("Gets every country.");

    private static Ok<IReadOnlyList<CountryDto>> Handle([FromServices] AtlasStore store) =>
        TypedResults.Ok(store.Countries);
}
