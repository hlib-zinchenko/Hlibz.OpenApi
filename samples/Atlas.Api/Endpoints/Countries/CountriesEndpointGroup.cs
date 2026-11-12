using Hlibz.MinimalApi.Endpoints;

namespace Atlas.Api.Endpoints.Countries;

internal sealed class CountriesEndpointGroup : IEndpointGroup
{
    public string Prefix => "api/countries";

    public void Configure(RouteGroupBuilder group) => group.WithTags("Countries");
}
