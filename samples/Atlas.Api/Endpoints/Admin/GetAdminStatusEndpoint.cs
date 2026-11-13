using Atlas.Api.Domain;

using Hlibz.MinimalApi.Endpoints;

using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.Api.Endpoints.Admin;

/// <summary>
/// Gets how many countries, cities and pending suggestions the atlas holds. Open to everyone,
/// as a health check.
/// </summary>
/// <remarks>
/// Its group requires the admin policy, but <c>AllowAnonymous()</c> opens it again, so the docs
/// show no lock on it.
/// </remarks>
internal sealed class GetAdminStatusEndpoint : IEndpoint<AdminAtlasEndpointGroup>
{
    public RouteHandlerBuilder Map(IEndpointRouteBuilder group) =>
        group.MapGet("status", Handle)
            .WithSummary("Gets the admin API's status.")
            .AllowAnonymous();

    private static Ok<AdminStatusDto> Handle([FromServices] AtlasStore store) =>
        TypedResults.Ok(new AdminStatusDto(
            store.Countries.Count,
            store.Cities.Count,
            store.PendingSuggestions));
}
