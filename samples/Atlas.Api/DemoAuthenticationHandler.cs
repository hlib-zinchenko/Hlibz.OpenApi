using System.Security.Claims;
using System.Text.Encodings.Web;

using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Atlas.Api;

/// <summary>
/// A stand-in for real authentication, so the sample runs with no identity provider. It takes the
/// two credentials the OpenAPI documents describe: <c>Authorization: Bearer demo-user</c> or
/// <c>Bearer demo-admin</c> (the bearer scheme, in both documents), and
/// <c>X-Api-Key: demo-admin-key</c> (the API key scheme, in the admin document only). Anything
/// else is anonymous.
/// </summary>
internal sealed class DemoAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Demo";

    public const string ApiKeyHeader = "X-Api-Key";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        string? role = Request.Headers.Authorization.ToString() switch
        {
            "Bearer demo-user" => "user",
            "Bearer demo-admin" => "admin",
            _ => Request.Headers[ApiKeyHeader].ToString() == "demo-admin-key" ? "admin" : null,
        };
        if (role is null)
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        ClaimsIdentity identity = new(
            [new Claim(ClaimTypes.Name, $"demo-{role}"), new Claim(ClaimTypes.Role, role)],
            SchemeName);
        return Task.FromResult(AuthenticateResult.Success(
            new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
    }
}
