using Atlas.Api.Domain;

using Hlibz.MinimalApi.Endpoints;

using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.Api.Endpoints.Cities;

/// <summary>
/// Suggests a city the atlas is missing, for an admin to review. Needs a signed-in user.
/// </summary>
/// <remarks>
/// The <c>Member</c> policy puts a lock on this endpoint, and only this one, in the public
/// document. <c>countryId</c> carries <c>[NotDefault]</c>: a <c>not</c> keyword and a sentence
/// on the inlined ID.
/// </remarks>
internal sealed class SuggestCityEndpoint : IEndpoint<CitiesEndpointGroup>
{
    public RouteHandlerBuilder Map(IEndpointRouteBuilder group) =>
        group.MapPost("suggestions", Handle)
            .WithSummary("Suggests a missing city.")
            .RequireAuthorization(Policies.Member);

    private static Accepted Handle(SuggestCityRequest request, [FromServices] AtlasStore store)
    {
        store.Suggest(request);
        return TypedResults.Accepted((string?)null);
    }
}
