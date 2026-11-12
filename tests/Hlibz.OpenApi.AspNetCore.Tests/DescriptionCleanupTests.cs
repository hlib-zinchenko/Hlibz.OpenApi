using System.Text.Json;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Hlibz.OpenApi.AspNetCore.Tests;

public sealed class DescriptionCleanupTests
{
    [Fact]
    public async Task AddDescriptionCleanup_ByDefault_StripsAccessorWording()
    {
        JsonElement document = await StartAsync(options => options.AddDescriptionCleanup());

        JsonElement properties = document.Schema("ChangePassword").GetProperty("properties");
        Assert.Equal("The user's new password.", properties.GetProperty("password").GetProperty("description").GetString());
        Assert.Equal(
            "Whether every other session is signed out.",
            properties.GetProperty("signOutEverywhere").GetProperty("description").GetString());
        Assert.Equal("Changes a user's password.", document.Schema("ChangePassword").GetProperty("description").GetString());
        Assert.Equal(
            "The most rows to return.",
            document.Operation("/search").Parameter("Limit").GetProperty("description").GetString());
    }

    [Fact]
    public async Task AddDescriptionCleanup_PropertyNextToReference_IsStripped()
    {
        JsonElement document = await StartAsync(options => options.AddDescriptionCleanup());

        Assert.Equal(
            "The paint's colour.",
            document.Schema("Paint").GetProperty("properties").GetProperty("colour").GetProperty("description").GetString());
    }

    [Fact]
    public async Task AddDescriptionCleanup_OperationSummary_IsKept()
    {
        JsonElement document = await StartAsync(options => options.AddDescriptionCleanup());

        Assert.Equal("Gets the paint.", document.Operation("/paint").GetProperty("summary").GetString());
    }

    [Fact]
    public async Task AddDescriptionCleanup_WithCustomPrefixes_UsesThem()
    {
        JsonElement document = await StartAsync(options => options.AddDescriptionCleanup(cleanup =>
        {
            cleanup.Prefixes.Clear();
            cleanup.Prefixes.Add("Changes ");
        }));

        Assert.Equal("A user's password.", document.Schema("ChangePassword").GetProperty("description").GetString());
        Assert.Equal(
            "Gets or sets the user's new password.",
            document.Schema("ChangePassword").GetProperty("properties").GetProperty("password").GetProperty("description").GetString());
    }

    private static async Task<JsonElement> StartAsync(Action<Microsoft.AspNetCore.OpenApi.OpenApiOptions> configure)
    {
        HttpClient client = await TestApp.StartAsync(
            configure,
            app =>
            {
                app.MapPost("password", (ChangePassword body) => TypedResults.NoContent());
                app.MapGet("search", ([AsParameters] SearchQuery query) => TypedResults.Ok());
                app.MapGet("paint", () => TypedResults.Ok(new Paint())).WithSummary("Gets the paint.");
            });
        return await client.GetDocumentAsync();
    }
}
