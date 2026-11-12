using Hlibz.MinimalApi.Endpoints;

namespace Atlas.Api.Endpoints.Admin;

/// <summary>The admin API: every endpoint requires the admin policy, unless it opts out.</summary>
internal sealed class AdminAtlasEndpointGroup : IAdminEndpointGroup
{
    public string Prefix => "api/admin";

    public void Configure(RouteGroupBuilder group) =>
        group.WithTags("Atlas admin").RequireAuthorization(Policies.Admin);
}
