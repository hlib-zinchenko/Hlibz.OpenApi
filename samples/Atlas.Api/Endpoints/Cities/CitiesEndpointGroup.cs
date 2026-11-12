using Hlibz.MinimalApi.Endpoints;

namespace Atlas.Api.Endpoints.Cities;

internal sealed class CitiesEndpointGroup : IEndpointGroup
{
    public string Prefix => "api/cities";

    public void Configure(RouteGroupBuilder group) => group.WithTags("Cities");
}
