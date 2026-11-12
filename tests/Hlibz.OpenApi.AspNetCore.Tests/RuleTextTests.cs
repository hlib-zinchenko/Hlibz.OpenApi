using System.Text.Json.Nodes;

using Microsoft.OpenApi;

namespace Hlibz.OpenApi.AspNetCore.Tests;

public sealed class RuleTextTests
{
    [Fact]
    public void Add_WithoutDescription_SetsDescriptionAndList()
    {
        OpenApiSchema schema = new();

        RuleText.Add(schema, "One.");

        Assert.Equal("One.", schema.Description);
        Assert.Equal(["One."], RuleText.Read(schema));
    }

    [Fact]
    public void Add_WithDescription_AppendsParagraph()
    {
        OpenApiSchema schema = new() { Description = "The name." };

        RuleText.Add(schema, "One.");
        RuleText.Add(schema, "Two.");

        Assert.Equal("The name.\n\nOne.\n\nTwo.", schema.Description);
        Assert.Equal(["One.", "Two."], RuleText.Read(schema));
    }

    [Fact]
    public void Add_SameTextTwice_AddsItOnce()
    {
        OpenApiSchema schema = new();

        RuleText.Add(schema, "One.");
        RuleText.Add(schema, "One.");

        Assert.Equal("One.", schema.Description);
        JsonNodeExtension extension = Assert.IsType<JsonNodeExtension>(schema.Extensions![RuleText.ExtensionName]);
        Assert.Single(Assert.IsType<JsonArray>(extension.Node));
    }

    [Fact]
    public void Add_WithBlankText_Throws()
    {
        Assert.Throws<ArgumentException>(() => RuleText.Add(new OpenApiSchema(), " "));
    }

    [Fact]
    public void Remove_LastParagraph_ReturnsNull()
    {
        Assert.Null(RuleText.Remove("One.", "One."));
        Assert.Equal("Name.", RuleText.Remove("Name.\n\nOne.", "One."));
    }
}
