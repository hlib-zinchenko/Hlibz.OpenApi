using System.Text.Json;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;

namespace Hlibz.OpenApi.AspNetCore.Tests;

public sealed class SecurityTests
{
    [Fact]
    public async Task AddBearerSecurity_ByDefault_AddsJwtBearerScheme()
    {
        JsonElement document = await StartAsync(options => options.AddBearerSecurity());

        JsonElement scheme = SecurityScheme(document, "Bearer");
        Assert.Equal("http", scheme.GetProperty("type").GetString());
        Assert.Equal("bearer", scheme.GetProperty("scheme").GetString());
        Assert.Equal("JWT", scheme.GetProperty("bearerFormat").GetString());
    }

    [Fact]
    public async Task AddBearerSecurity_MarksOnlyEndpointsThatRequireAuthorization()
    {
        JsonElement document = await StartAsync(options => options.AddBearerSecurity());

        Assert.Equal(["Bearer"], Schemes(document, "/authorized"));
        Assert.Equal(["Bearer"], Schemes(document, "/policy"));
        Assert.Equal(["Bearer"], Schemes(document, "/group/inside"));
        Assert.Empty(Schemes(document, "/open"));
        Assert.Empty(Schemes(document, "/group/anonymous"));
    }

    [Fact]
    public async Task AddBearerSecurity_WithFallbackPolicy_MarksEveryNonAnonymousEndpoint()
    {
        JsonElement document = await StartAsync(
            options => options.AddBearerSecurity(),
            services => services.AddAuthorizationBuilder()
                .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build()));

        Assert.Equal(["Bearer"], Schemes(document, "/open"));
        Assert.Empty(Schemes(document, "/group/anonymous"));
    }

    [Fact]
    public async Task AddBearerSecurity_WithOptions_UsesThem()
    {
        JsonElement document = await StartAsync(options => options.AddBearerSecurity(bearer =>
        {
            bearer.SchemeName = "AccessToken";
            bearer.BearerFormat = null;
            bearer.Description = "From POST /login.";
        }));

        JsonElement scheme = SecurityScheme(document, "AccessToken");
        Assert.False(scheme.TryGetProperty("bearerFormat", out _));
        Assert.Equal("From POST /login.", scheme.GetProperty("description").GetString());
        Assert.Equal(["AccessToken"], Schemes(document, "/authorized"));
    }

    [Fact]
    public async Task AddSecurity_SeveralSchemes_ListsEachAsAlternative()
    {
        JsonElement document = await StartAsync(options => options
            .AddBearerSecurity()
            .AddSecurity("ApiKey", new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.ApiKey,
                In = ParameterLocation.Header,
                Name = "X-Api-Key",
            }));

        Assert.Equal("apiKey", SecurityScheme(document, "ApiKey").GetProperty("type").GetString());
        Assert.Equal(["Bearer", "ApiKey"], Schemes(document, "/authorized"));
    }

    [Fact]
    public void RequiresAuthorization_DecidesFromMetadataAndFallbackPolicy()
    {
        AuthorizationPolicy policy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();

        Assert.True(Internal.SecurityRequirementOperationTransformer.RequiresAuthorization([new AuthorizeAttribute()], null));
        Assert.True(Internal.SecurityRequirementOperationTransformer.RequiresAuthorization([policy], null));
        Assert.True(Internal.SecurityRequirementOperationTransformer.RequiresAuthorization([], policy));
        Assert.False(Internal.SecurityRequirementOperationTransformer.RequiresAuthorization([], null));
        Assert.False(Internal.SecurityRequirementOperationTransformer.RequiresAuthorization(
            [new AuthorizeAttribute(), new AllowAnonymousAttribute()], null));
        Assert.False(Internal.SecurityRequirementOperationTransformer.RequiresAuthorization([new AllowAnonymousAttribute()], policy));
    }

    private static async Task<JsonElement> StartAsync(
        Action<Microsoft.AspNetCore.OpenApi.OpenApiOptions> configure,
        Action<IServiceCollection>? services = null)
    {
        HttpClient client = await TestApp.StartAsync(
            configure,
            app =>
            {
                app.MapGet("open", () => TypedResults.Ok());
                app.MapGet("authorized", () => TypedResults.Ok()).RequireAuthorization();
                app.MapGet("policy", () => TypedResults.Ok())
                    .RequireAuthorization(new AuthorizationPolicyBuilder().RequireRole("admin").Build());
                RouteGroupBuilder group = app.MapGroup("group").RequireAuthorization();
                group.MapGet("inside", () => TypedResults.Ok());
                group.MapGet("anonymous", () => TypedResults.Ok()).AllowAnonymous();
            },
            services);
        return await client.GetDocumentAsync();
    }

    private static JsonElement SecurityScheme(JsonElement document, string name) =>
        document.GetProperty("components").GetProperty("securitySchemes").GetProperty(name);

    private static string[] Schemes(JsonElement document, string path) =>
        document.Operation(path).TryGetProperty("security", out JsonElement security)
            ? [.. security.EnumerateArray().SelectMany(requirement => requirement.EnumerateObject().Select(scheme => scheme.Name))]
            : [];
}
