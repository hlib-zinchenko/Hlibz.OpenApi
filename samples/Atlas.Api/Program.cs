// Atlas: a small geography API showing everything Hlibz.OpenApi.AspNetCore and
// Hlibz.OpenApi.Results do, on plain minimal API endpoints, documented with Hlibz.Redoc.AspNetCore.
// Run it and open http://localhost:5290/api-docs. samples/README.md says what to look for where.

using Atlas.Api;
using Atlas.Api.Domain;
using Atlas.Api.Endpoints;

using Hlibz.OpenApi;
using Hlibz.Redoc.Extensions;
using Hlibz.Results.AspNetCore;

using Microsoft.OpenApi;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<AtlasStore>();
builder.Services.AddValidation();

// The error body every Result failure is sent with: its code, message and details.
builder.Services.AddResultErrorResponses();

// Validation failures are written through it, as application/problem+json.
builder.Services.AddProblemDetails();

// Microsoft.AspNetCore.OpenApi builds the document again on every request; cache it instead.
builder.Services.AddOutputCache();
builder.Services
    .AddAuthentication(DemoAuthenticationHandler.SchemeName)
    .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, DemoAuthenticationHandler>(
        DemoAuthenticationHandler.SchemeName,
        _ => { });
builder.Services.AddAuthorizationBuilder()
    .AddPolicy(Policies.Member, policy => policy.RequireAuthenticatedUser())
    .AddPolicy(Policies.Admin, policy => policy.RequireRole("admin"));

// Two documents, one per endpoint group name, configured once. The callback is the app's own
// AddOpenApi call, as Microsoft.AspNetCore.OpenApi's XML comment generator needs.
IReadOnlyList<OpenApiDocumentDefinition> documents = builder.Services.AddOpenApiDocuments(
    Documents.All,
    (services, name, configure) => services.AddOpenApi(name, configure),
    options =>
    {
        options
            .AddXmlDocumentation()
            .AddStronglyTypedIds()
            .AddOpenApiRules()
            .AddDescriptionCleanup()
            .AddValidationProblems()
            .AddResults()
            .AddBearerSecurity(bearer =>
            {
                bearer.BearerFormat = null;
                bearer.Description =
                    "A demo token: `demo-user` for any signed-in endpoint, `demo-admin` for the admin API.";
            });

        // Only the admin API takes an API key, so only its document describes one.
        if (options.DocumentName == Documents.Admin.Name)
        {
            options.AddSecurity("ApiKey", new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.ApiKey,
                In = ParameterLocation.Header,
                Name = DemoAuthenticationHandler.ApiKeyHeader,
                Description = "`demo-admin-key`, for scripts that can't hold a token.",
            });
        }
    });

WebApplication app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();
app.UseOutputCache();

app.MapOpenApi().CacheOutput();

// Each group names the document for its audience (WithGroupName); AddOpenApiDocuments filters on it.
app.MapCountries();
app.MapCities();
app.MapAdmin();

// The same list feeds ReDoc's picker. showExtensions makes each rule's x-rules list visible.
app.MapReDocDocuments(
    documents.Select(document => new ReDocDocument(document.Name, document.Title)),
    docs =>
    {
        docs.DefaultColorMode = ReDocColorMode.System;
        docs.AdditionalConfigJson = """{ "showExtensions": ["x-rules"] }""";
    });

// No group name, so it's in neither document.
app.MapGet("/", () => Results.Redirect("/api-docs"));

await app.RunAsync();
