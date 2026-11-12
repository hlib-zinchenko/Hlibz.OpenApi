using System.Text.Json;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Hlibz.OpenApi.AspNetCore.Tests;

public sealed class StronglyTypedIdTests
{
    [Fact]
    public async Task GuidId_ByDefault_IsInlinedUuidString()
    {
        JsonElement document = await StartAsync();

        JsonElement countryId = CityProperty(document, "countryId");
        Assert.Equal("string", countryId.GetProperty("type").GetString());
        Assert.Equal("uuid", countryId.GetProperty("format").GetString());
        Assert.False(document.HasComponent("CountryId"));
    }

    [Fact]
    public async Task NullableId_ByDefault_IsNullableUuidStringWithoutOneOf()
    {
        JsonElement document = await StartAsync();

        JsonElement previous = CityProperty(document, "previousCountryId");
        Assert.Equal(["null", "string"], previous.GetProperty("type").Strings());
        Assert.Equal("uuid", previous.GetProperty("format").GetString());
        Assert.False(previous.TryGetProperty("oneOf", out _));
    }

    [Fact]
    public async Task IntId_ByDefault_TakesIntSchema()
    {
        JsonElement document = await StartAsync();

        JsonElement number = CityProperty(document, "number");
        Assert.Contains("integer", number.GetProperty("type").Strings());
        Assert.Equal("int32", number.GetProperty("format").GetString());
        Assert.False(number.TryGetProperty("properties", out _));
    }

    [Fact]
    public async Task StructWithoutConverter_ByDefault_StaysAnObject()
    {
        JsonElement document = await StartAsync();

        Assert.True(CityProperty(document, "plain").TryGetProperty("$ref", out _));
        Assert.Equal("object", document.Schema("PlainWrapper").GetProperty("type").GetString());
    }

    [Fact]
    public async Task RouteParameter_ByDefault_IsUuidString()
    {
        JsonElement document = await StartAsync();

        JsonElement id = document.Operation("/cities/{id}").Parameter("id").GetProperty("schema");
        Assert.Equal("string", id.GetProperty("type").GetString());
        Assert.Equal("uuid", id.GetProperty("format").GetString());
    }

    [Fact]
    public async Task QueryArrayOfIds_ByDefault_ItemsAreUuidStrings()
    {
        HttpClient client = await TestApp.StartAsync(
            options => options.AddStronglyTypedIds(),
            app => app.MapGet("search", ([AsParameters] SearchQuery query) => TypedResults.Ok()));
        JsonElement document = await client.GetDocumentAsync();

        JsonElement schema = document.Operation("/search").Parameter("Countries").GetProperty("schema");
        JsonElement type = schema.GetProperty("type");
        string[] types = type.ValueKind == JsonValueKind.Array ? type.Strings() : [type.GetString()!];
        Assert.Contains("array", types);
        Assert.Equal("uuid", schema.GetProperty("items").GetProperty("format").GetString());
    }

    [Fact]
    public async Task CustomRule_OnlyRewritesTypesItMatches()
    {
        HttpClient client = await TestApp.StartAsync(
            options => options.AddStronglyTypedIds(type => type == typeof(PlainWrapper) ? typeof(Guid) : null),
            MapCity);
        JsonElement document = await client.GetDocumentAsync();

        Assert.Equal("uuid", CityProperty(document, "plain").GetProperty("format").GetString());
        Assert.False(CityProperty(document, "countryId").TryGetProperty("format", out _));
    }

    [Fact]
    public void SingleValueStruct_MatchesOnlyStructsWithOneValueProperty()
    {
        Assert.Equal(typeof(Guid), Internal.StronglyTypedIdSchemaTransformer.SingleValueStruct(typeof(CountryId)));
        Assert.Null(Internal.StronglyTypedIdSchemaTransformer.SingleValueStruct(typeof(Guid)));
        Assert.Null(Internal.StronglyTypedIdSchemaTransformer.SingleValueStruct(typeof(int)));
        Assert.Null(Internal.StronglyTypedIdSchemaTransformer.SingleValueStruct(typeof(Colour)));
        Assert.Null(Internal.StronglyTypedIdSchemaTransformer.SingleValueStruct(typeof(DateRange)));
        Assert.Null(Internal.StronglyTypedIdSchemaTransformer.SingleValueStruct(typeof(KeyValuePair<int, int>)));
    }

    private static async Task<JsonElement> StartAsync()
    {
        HttpClient client = await TestApp.StartAsync(options => options.AddStronglyTypedIds(), MapCity);
        return await client.GetDocumentAsync();
    }

    private static void MapCity(WebApplication app) =>
        app.MapGet(
            "cities/{id}",
            (CountryId id) => TypedResults.Ok(new City(id, null, new OrderNumber(1), new PlainWrapper(Guid.Empty), "x")));

    private static JsonElement CityProperty(JsonElement document, string name) =>
        document.Schema("City").GetProperty("properties").GetProperty(name);
}
