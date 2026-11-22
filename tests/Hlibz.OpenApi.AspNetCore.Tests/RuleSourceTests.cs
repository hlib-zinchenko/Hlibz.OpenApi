using System.Reflection;
using System.Text.Json;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.OpenApi;

namespace Hlibz.OpenApi.AspNetCore.Tests;

public sealed class RuleSourceTests
{
    [Fact]
    public async Task RecordParameters_WithDataAnnotations_WriteTheirKeywords()
    {
        JsonElement document = await DocumentAsync(options => options.AddOpenApiRules());
        JsonElement properties = document.Schema("BookTable").GetProperty("properties");

        Assert.Equal(1, properties.GetProperty("seats").GetProperty("minimum").GetInt32());
        Assert.Equal(5, properties.GetProperty("seats").GetProperty("maximum").GetInt32());
        Assert.Equal(2, properties.GetProperty("note").GetProperty("minLength").GetInt32());
        Assert.Equal(20, properties.GetProperty("note").GetProperty("maxLength").GetInt32());
        Assert.Equal(1, properties.GetProperty("guests").GetProperty("minItems").GetInt32());
        Assert.Equal(3, properties.GetProperty("guests").GetProperty("maxItems").GetInt32());
        Assert.Equal(0, properties.GetProperty("deposit").GetProperty("exclusiveMinimum").GetInt32());
        Assert.Equal(1, properties.GetProperty("deposit").GetProperty("maximum").GetInt32());
        Assert.Equal("^[a-z]+$", properties.GetProperty("slug").GetProperty("pattern").GetString());
        Assert.Equal("uri", properties.GetProperty("site").GetProperty("format").GetString());
    }

    [Fact]
    public async Task RecordParameter_WithRuleAttribute_WritesRule()
    {
        JsonElement document = await DocumentAsync(options => options.AddOpenApiRules());

        JsonElement code = document.Schema("BookTable").GetProperty("properties").GetProperty("code");
        Assert.Equal([StrongPasswordAttribute.Text], code.GetProperty(RuleText.ExtensionName).Strings());
    }

    [Fact]
    public async Task RecordParameters_WithoutAddOpenApiRules_AreLeftOut()
    {
        JsonElement document = await DocumentAsync(_ => { });

        JsonElement seats = document.Schema("BookTable").GetProperty("properties").GetProperty("seats");
        Assert.False(seats.TryGetProperty("minimum", out _));
    }

    [Fact]
    public async Task Source_OnTypePropertyAndRecordParameter_WritesItsRules()
    {
        JsonElement document = await DocumentAsync(options => options.AddOpenApiRules(AddPlainRules));
        JsonElement meeting = document.Schema("Meeting");
        JsonElement properties = meeting.GetProperty("properties");

        Assert.Equal(["Only on weekdays."], meeting.GetProperty(RuleText.ExtensionName).Strings());
        Assert.Equal(["Ten people at most."], properties.GetProperty("people").GetProperty(RuleText.ExtensionName).Strings());
        Assert.Equal(["Never the same name twice."], properties.GetProperty("name").GetProperty(RuleText.ExtensionName).Strings());
    }

    [Fact]
    public async Task Source_OnComponentTypedProperty_DescribesReferenceOnly()
    {
        JsonElement document = await DocumentAsync(options => options.AddOpenApiRules(AddPlainRules));

        JsonElement colour = document.Schema("Meeting").GetProperty("properties").GetProperty("colour");
        Assert.True(colour.TryGetProperty("$ref", out _));
        Assert.EndsWith("Bring a pen.", colour.GetProperty("description").GetString(), StringComparison.Ordinal);
        Assert.False(document.Schema("Colour").TryGetProperty(RuleText.ExtensionName, out _));
    }

    [Fact]
    public async Task AddOpenApiRules_CalledTwice_WritesEachRuleOnceWithBothSources()
    {
        JsonElement document = await DocumentAsync(options => options
            .AddOpenApiRules()
            .AddOpenApiRules(AddPlainRules)
            .AddOpenApiRules());
        JsonElement properties = document.Schema("BookTable").GetProperty("properties");

        Assert.Equal(
            [StrongPasswordAttribute.Text],
            properties.GetProperty("code").GetProperty(RuleText.ExtensionName).Strings());
        Assert.Equal(
            ["Ten people at most."],
            document.Schema("Meeting").GetProperty("properties").GetProperty("people").GetProperty(RuleText.ExtensionName).Strings());
    }

    private static void AddPlainRules(OpenApiRuleOptions rules) =>
        rules.Sources.Add(provider => provider
            .GetCustomAttributes(inherit: true)
            .OfType<PlainRuleAttribute>()
            .Select(rule => new TextRule(rule.Text)));

    private static async Task<JsonElement> DocumentAsync(Action<Microsoft.AspNetCore.OpenApi.OpenApiOptions> configure)
    {
        HttpClient client = await TestApp.StartAsync(
            configure,
            app =>
            {
                app.MapPost("tables", (BookTable table) => TypedResults.Ok(table));
                app.MapPost("meetings", (Meeting meeting) => TypedResults.Ok(meeting));
            });
        return await client.GetDocumentAsync();
    }

    private sealed class TextRule(string text) : IOpenApiRule
    {
        public void Describe(OpenApiSchema schema) => RuleText.Add(schema, text);
    }
}
