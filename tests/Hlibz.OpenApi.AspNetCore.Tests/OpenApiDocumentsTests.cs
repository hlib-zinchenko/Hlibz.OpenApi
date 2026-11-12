using System.Text.Json;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Hlibz.OpenApi.AspNetCore.Tests;

public sealed class OpenApiDocumentsTests
{
    private static readonly OpenApiDocumentDefinition[] Documents =
    [
        new("locations", "Locations"),
        new("locations-admin", "LocationsAdmin", "Locations (Admin)"),
    ];

    [Fact]
    public async Task AddOpenApiDocuments_EachDocument_HoldsOnlyItsGroup()
    {
        HttpClient client = await StartAsync();

        JsonElement locations = await client.GetDocumentAsync("locations");
        JsonElement admin = await client.GetDocumentAsync("locations-admin");

        Assert.Equal(["/cities"], Paths(locations));
        Assert.Equal(["/admin/cities"], Paths(admin));
    }

    [Fact]
    public async Task AddOpenApiDocuments_WithTitle_SetsInfoTitle()
    {
        HttpClient client = await StartAsync();

        JsonElement admin = await client.GetDocumentAsync("locations-admin");

        Assert.Equal("Locations (Admin)", admin.GetProperty("info").GetProperty("title").GetString());
    }

    [Fact]
    public async Task AddOpenApiDocuments_ConfigureOptions_AppliesToEveryDocument()
    {
        HttpClient client = await StartAsync();

        foreach (OpenApiDocumentDefinition definition in Documents)
        {
            JsonElement document = await client.GetDocumentAsync(definition.Name);
            Assert.True(document.GetProperty("components").GetProperty("securitySchemes").TryGetProperty("Bearer", out _));
        }
    }

    [Fact]
    public async Task AddOpenApiDocuments_ThroughAppsOwnAddOpenApi_KeepsXmlComments()
    {
        HttpClient client = await StartAsync();

        JsonElement locations = await client.GetDocumentAsync("locations");

        Assert.Equal("A date range.", locations.Schema("DateRange").GetProperty("description").GetString());
    }

    [Fact]
    public void AddOpenApiDocuments_ReturnsDefinitionsInOrder()
    {
        ServiceCollection services = new();

        IReadOnlyList<OpenApiDocumentDefinition> registered = services.AddOpenApiDocuments(
            Documents,
            (collection, name, configure) => collection.AddOpenApi(name, configure));

        Assert.Equal(Documents, registered);
    }

    [Fact]
    public void AddOpenApiDocuments_DuplicateName_Throws()
    {
        ServiceCollection services = new();

        ArgumentException exception = Assert.Throws<ArgumentException>(() => services.AddOpenApiDocuments(
            [new("locations"), new("Locations", "Other")],
            (collection, name, configure) => collection.AddOpenApi(name, configure)));

        Assert.Contains("locations", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void EffectiveGroupName_WithoutGroupName_IsName()
    {
        Assert.Equal("taxes", new OpenApiDocumentDefinition("taxes").EffectiveGroupName);
        Assert.Equal("Taxes", new OpenApiDocumentDefinition("taxes", "Taxes").EffectiveGroupName);
    }

    private static Task<HttpClient> StartAsync() =>
        TestApp.StartAsync(
            services => services.AddOpenApiDocuments(
                Documents,
                (collection, name, configure) => collection.AddOpenApi(name, configure),
                options => options.AddBearerSecurity()),
            app =>
            {
                app.MapGet("cities", () => TypedResults.Ok(new DateRange(default, default))).WithGroupName("Locations");
                app.MapGet("admin/cities", () => TypedResults.Ok()).WithGroupName("LocationsAdmin");
                app.MapGet("ungrouped", () => TypedResults.Ok());
            });

    private static string[] Paths(JsonElement document) =>
        [.. document.GetProperty("paths").EnumerateObject().Select(path => path.Name)];
}
