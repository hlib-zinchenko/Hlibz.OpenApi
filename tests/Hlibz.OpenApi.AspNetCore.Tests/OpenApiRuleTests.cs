using System.Text.Json;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Hlibz.OpenApi.AspNetCore.Tests;

public sealed class OpenApiRuleTests
{
    [Fact]
    public async Task BodyProperty_WithRuleAttribute_WritesKeywordAndAppendsTextToSummary()
    {
        JsonElement document = await StartAsync();

        JsonElement password = document.Schema("ChangePassword").GetProperty("properties").GetProperty("password");
        Assert.Equal("^(?=.*[a-z])(?=.*[A-Z])(?=.*\\d).*$", password.GetProperty("pattern").GetString());
        Assert.Equal(8, password.GetProperty("minLength").GetInt32());
        Assert.Equal(
            $"Gets or sets the user's new password.\n\n{StrongPasswordAttribute.Text}",
            password.GetProperty("description").GetString());
        Assert.Equal([StrongPasswordAttribute.Text], password.GetProperty(RuleText.ExtensionName).Strings());
    }

    [Fact]
    public async Task Type_WithOpenApiRuleAttribute_AddsTextToTypeSchema()
    {
        JsonElement document = await StartAsync();

        JsonElement range = document.Schema("DateRange");
        Assert.Equal(
            "A date range.\n\nEnd must not be earlier than Start.",
            range.GetProperty("description").GetString());
        Assert.False(range.GetProperty("properties").GetProperty("start").TryGetProperty("description", out _));
    }

    [Fact]
    public async Task AsParametersProperty_WithRule_PutsTextInParameterDescriptionAndKeepsList()
    {
        JsonElement document = await StartAsync();

        JsonElement limit = document.Operation("/search").Parameter("Limit");
        Assert.Equal(
            "Gets or sets the most rows to return.\n\nMust be a multiple of 10.",
            limit.GetProperty("description").GetString());
        JsonElement schema = limit.GetProperty("schema");
        Assert.False(schema.TryGetProperty("description", out _));
        Assert.Equal(["Must be a multiple of 10."], schema.GetProperty(RuleText.ExtensionName).Strings());
    }

    [Fact]
    public async Task AsParametersArray_WithRule_DescribesArrayNotItems()
    {
        JsonElement document = await StartAsync();

        JsonElement tags = document.Operation("/search").Parameter("Tags");
        Assert.Equal("No tag twice.", tags.GetProperty("description").GetString());
        JsonElement schema = tags.GetProperty("schema");
        Assert.Equal(["No tag twice."], schema.GetProperty(RuleText.ExtensionName).Strings());
        Assert.False(schema.GetProperty("items").TryGetProperty(RuleText.ExtensionName, out _));
        Assert.False(schema.GetProperty("items").TryGetProperty("description", out _));
    }

    [Fact]
    public async Task AsParametersType_WithRule_AddsTextToOperationDescription()
    {
        JsonElement document = await StartAsync();

        JsonElement search = document.Operation("/search");
        Assert.Equal("Give at most one of Tags and Countries.", search.GetProperty("description").GetString());
        Assert.False(document.Operation("/password", "post").TryGetProperty("description", out _));
    }

    [Fact]
    public async Task HandlerParameter_WithRule_DescribesParameter()
    {
        JsonElement document = await StartAsync();

        JsonElement id = document.Operation("/countries/{id}").Parameter("id");
        Assert.Equal(NotDefaultAttribute.Text, id.GetProperty("description").GetString());
        Assert.Equal(
            "00000000-0000-0000-0000-000000000000",
            id.GetProperty("schema").GetProperty("not").GetProperty("const").GetString());
    }

    [Fact]
    public async Task ComponentTypedProperty_WithRule_DescribesReferenceNotSharedComponent()
    {
        JsonElement document = await StartAsync();

        JsonElement colour = document.Schema("Paint").GetProperty("properties").GetProperty("colour");
        Assert.True(colour.TryGetProperty("$ref", out _));
        Assert.Equal(
            "Gets or sets the paint's colour.\n\nGreen is out of stock.",
            colour.GetProperty("description").GetString());
        Assert.False(document.Schema("Colour").TryGetProperty("description", out _));
        Assert.False(document.Schema("Colour").TryGetProperty(RuleText.ExtensionName, out _));
        Assert.False(document.Schema("Paint").GetProperty("properties").GetProperty("trim").TryGetProperty("description", out _));
    }

    [Fact]
    public async Task StronglyTypedIdProperty_WithRule_KeepsKeywordsOnInlinedId()
    {
        JsonElement document = await StartAsync();

        JsonElement madeIn = document.Schema("Paint").GetProperty("properties").GetProperty("madeIn");
        Assert.Equal("uuid", madeIn.GetProperty("format").GetString());
        Assert.Equal(
            $"Gets or sets the country the paint is made in.\n\n{NotDefaultAttribute.Text}",
            madeIn.GetProperty("description").GetString());
        Assert.True(madeIn.TryGetProperty("not", out _));
        Assert.Equal([NotDefaultAttribute.Text], madeIn.GetProperty(RuleText.ExtensionName).Strings());
    }

    [Fact]
    public async Task StronglyTypedIdProperty_IdsRegisteredFirst_GivesSameSchema()
    {
        HttpClient client = await TestApp.StartAsync(
            options => options.AddStronglyTypedIds().AddOpenApiRules(),
            app => app.MapPost("paint", (Paint paint) => TypedResults.Ok(paint)));
        JsonElement document = await client.GetDocumentAsync();

        JsonElement madeIn = document.Schema("Paint").GetProperty("properties").GetProperty("madeIn");
        Assert.Equal(
            $"Gets or sets the country the paint is made in.\n\n{NotDefaultAttribute.Text}",
            madeIn.GetProperty("description").GetString());
        Assert.True(madeIn.TryGetProperty("not", out _));
    }

    [Fact]
    public async Task RuleAttribute_StillValidatesRequests()
    {
        HttpClient client = await TestApp.StartAsync(
            options => options.AddOpenApiRules(),
            app => app.MapPost(
                "password",
                (ChangePassword body) => TypedResults.NoContent()),
            TestApp.AddValidation);

        using StringContent weak = new("""{ "password": "alllowercase" }""", System.Text.Encoding.UTF8, "application/json");
        HttpResponseMessage response = await client.PostAsync(
            new Uri("/password", UriKind.Relative),
            weak,
            TestContext.Current.CancellationToken);

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static async Task<JsonElement> StartAsync()
    {
        HttpClient client = await TestApp.StartAsync(
            options => options.AddOpenApiRules().AddStronglyTypedIds(),
            app =>
            {
                app.MapPost("password", (ChangePassword body) => TypedResults.NoContent());
                app.MapPost("range", (DateRange range) => TypedResults.Ok(range));
                app.MapGet("search", ([AsParameters] SearchQuery query) => TypedResults.Ok());
                app.MapGet("countries/{id}", ([NotDefault] CountryId id) => TypedResults.Ok());
                app.MapPost("paint", (Paint paint) => TypedResults.Ok(paint));
            });
        return await client.GetDocumentAsync();
    }
}
