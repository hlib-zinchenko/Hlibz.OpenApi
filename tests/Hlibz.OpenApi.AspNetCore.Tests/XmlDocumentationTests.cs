using System.Text.Json;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OpenApi;

namespace Hlibz.OpenApi.AspNetCore.Tests;

public sealed class XmlDocumentationTests
{
    [Fact]
    public async Task AddXmlDocumentation_InternalBody_IsDescribed()
    {
        JsonElement document = await StartAsync(options => options.AddXmlDocumentation());

        JsonElement booking = document.Schema("BookRoom");
        Assert.Equal("Books a `room` for one night.", booking.GetProperty("description").GetString());
        Assert.Equal(
            "Gets or sets the guest's name.",
            booking.GetProperty("properties").GetProperty("guest").GetProperty("description").GetString());
    }

    [Fact]
    public async Task AddXmlDocumentation_InternalAsParameters_DescribesParameters()
    {
        JsonElement document = await StartAsync(options => options.AddXmlDocumentation());

        Assert.Equal(
            "Gets or sets the floor, from `0`.",
            document.Operation("/rooms").Parameter("Floor").GetProperty("description").GetString());
    }

    [Fact]
    public async Task AddXmlDocumentation_ParameterOfDocumentedType_GetsNoTypeSummary()
    {
        JsonElement document = await StartAsync(options => options.AddXmlDocumentation().AddStronglyTypedIds());

        JsonElement wing = document.Operation("/rooms").Parameter("wing");
        Assert.Equal("Gets or sets the wing.", wing.GetProperty("description").GetString());
        Assert.False(wing.GetProperty("schema").TryGetProperty("description", out _));
        Assert.Equal(
            "Identifies a wing of the hotel.",
            document.Schema("Wings").GetProperty("properties").GetProperty("wing").GetProperty("description").GetString());
    }

    [Fact]
    public async Task AddXmlDocumentation_PublicType_ReplacesGeneratorText()
    {
        JsonElement document = await StartAsync(options => options.AddXmlDocumentation());

        JsonElement house = document.Schema("House");
        Assert.Equal("A house on one line.", house.GetProperty("description").GetString());
        Assert.Equal(
            "Gets the owner, a ChangePassword.Password when it's `null`.",
            house.GetProperty("properties").GetProperty("owner").GetProperty("description").GetString());
    }

    [Fact]
    public async Task AddXmlDocumentation_RecordParameter_DescribesProperty()
    {
        JsonElement document = await StartAsync(options => options.AddXmlDocumentation());

        Assert.Equal(
            "The room's number.",
            document.Schema("Room").GetProperty("properties").GetProperty("number").GetProperty("description").GetString());
    }

    [Fact]
    public async Task AddXmlDocumentation_ReferencedAssembly_IsDescribed()
    {
        JsonElement document = await StartAsync(options => options.AddXmlDocumentation());

        Assert.Equal(
            "Gets or sets the scheme's description, e.g. where to get a token. Markdown is allowed.",
            document.Schema("BearerSecurityOptions").GetProperty("properties").GetProperty("description")
                .GetProperty("description").GetString());
    }

    [Fact]
    public async Task AddXmlDocumentation_PropertyWithReference_DescribesNextToIt()
    {
        JsonElement document = await StartAsync(options => options.AddXmlDocumentation());

        JsonElement paint = document.Schema("BookRoom").GetProperty("properties").GetProperty("paint");
        Assert.Equal("Gets or sets the paint to use.", ReferenceDescription(paint));
        Assert.False(document.Schema("Paint").TryGetProperty("description", out _));
    }

    [Fact]
    public async Task AddXmlDocumentation_WithoutSummary_LeavesDescriptionAlone()
    {
        JsonElement document = await StartAsync(options => options.AddXmlDocumentation());

        Assert.False(document.Schema("BookRoom").GetProperty("properties").GetProperty("nights").TryGetProperty("description", out _));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AddXmlDocumentation_WithRules_KeepsRuleTextInEitherOrder(bool rulesFirst)
    {
        JsonElement document = await StartAsync(options =>
        {
            if (rulesFirst)
            {
                options.AddOpenApiRules().AddXmlDocumentation();
            }
            else
            {
                options.AddXmlDocumentation().AddOpenApiRules();
            }
        });

        JsonElement properties = document.Schema("BookRoom").GetProperty("properties");
        Assert.Equal(
            "Gets or sets the guest's name.\n\nAt most 50 characters.",
            properties.GetProperty("guest").GetProperty("description").GetString());
        Assert.Equal(
            "Gets or sets the paint to use.\n\nMust be in stock.",
            ReferenceDescription(properties.GetProperty("paint")));
        Assert.Equal(
            "Gets or sets the floor, from `0`.\n\nMust be a multiple of 10.",
            document.Operation("/rooms").Parameter("Floor").GetProperty("description").GetString());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AddXmlDocumentation_WithStronglyTypedIds_DescribesIdInEitherOrder(bool idsFirst)
    {
        JsonElement document = await StartAsync(options =>
        {
            if (idsFirst)
            {
                options.AddOpenApiRules().AddStronglyTypedIds().AddXmlDocumentation();
            }
            else
            {
                options.AddOpenApiRules().AddXmlDocumentation().AddStronglyTypedIds();
            }
        });

        JsonElement country = document.Schema("BookRoom").GetProperty("properties").GetProperty("country");
        Assert.Equal("string", country.GetProperty("type").GetString());
        Assert.Equal(
            "Gets or sets the guest's country.\n\n" + NotDefaultAttribute.Text,
            country.GetProperty("description").GetString());
    }

    /// <summary>
    /// A nullable property's <c>$ref</c> is one branch of a <c>oneOf</c> with <c>null</c>, and
    /// ASP.NET Core puts the property's description on that branch.
    /// </summary>
    private static string? ReferenceDescription(JsonElement property) =>
        property.GetProperty("oneOf").EnumerateArray()
            .Single(branch => branch.TryGetProperty("$ref", out _))
            .GetProperty("description").GetString();

    private static async Task<JsonElement> StartAsync(Action<OpenApiOptions> configure)
    {
        HttpClient client = await TestApp.StartAsync(
            configure,
            app =>
            {
                app.MapPost("bookings", (BookRoom body) => TypedResults.NoContent());
                app.MapGet("rooms", ([AsParameters] RoomQuery query) => TypedResults.Ok(new Room(1)));
                app.MapGet("house", () => TypedResults.Ok(new House()));
                app.MapGet("bearer", () => TypedResults.Ok(new BearerSecurityOptions()));
                app.MapGet("wings", () => TypedResults.Ok(new Wings(new WingId(1))));
            });
        return await client.GetDocumentAsync();
    }
}

/// <summary>
/// Books a <c>room</c>
/// for one night.
/// </summary>
internal sealed class BookRoom
{
    /// <summary>Gets or sets the guest's name.</summary>
    [OpenApiRule("At most 50 characters.")]
    public string Guest { get; set; } = string.Empty;

    /// <summary>Gets or sets the guest's country.</summary>
    [NotDefault]
    public CountryId Country { get; set; }

    /// <summary>Gets or sets the paint to use.</summary>
    [OpenApiRule("Must be in stock.")]
    public Paint? Paint { get; set; }

    public int Nights { get; set; }
}

internal sealed class RoomQuery
{
    /// <summary>Gets or sets the wing.</summary>
    [Microsoft.AspNetCore.Mvc.FromQuery(Name = "wing")]
    public WingId? Wing { get; set; }

    /// <summary>Gets or sets the floor, from <c>0</c>.</summary>
    [OpenApiRule("Must be a multiple of 10.")]
    public int? Floor { get; set; }
}

/// <summary>Identifies a wing of the hotel.</summary>
[System.Text.Json.Serialization.JsonConverter(typeof(WingIdConverter))]
internal readonly record struct WingId(int Value) : IParsable<WingId>
{
    public static WingId Parse(string s, IFormatProvider? provider) => new(int.Parse(s, provider));

    public static bool TryParse(string? s, IFormatProvider? provider, out WingId result)
    {
        bool parsed = int.TryParse(s, provider, out int value);
        result = new WingId(value);
        return parsed;
    }
}

internal sealed class WingIdConverter : System.Text.Json.Serialization.JsonConverter<WingId>
{
    public override WingId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        new(reader.GetInt32());

    public override void Write(Utf8JsonWriter writer, WingId value, JsonSerializerOptions options) =>
        writer.WriteNumberValue(value.Value);
}

internal sealed record Wings(WingId Wing);

/// <summary>A room.</summary>
/// <param name="Number">The room's number.</param>
internal sealed record Room(int Number);

/// <summary>
/// A house
/// on one line.
/// </summary>
public sealed class House
{
    /// <summary>Gets the owner, a <see cref="ChangePassword.Password"/> when it's <see langword="null"/>.</summary>
    public string? Owner { get; init; }
}
