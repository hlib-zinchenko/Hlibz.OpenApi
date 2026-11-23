using System.Text.Json;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Hlibz.OpenApi.Paging.Tests;

public sealed class PagingOpenApiTests
{
    private const string SnakeCaseText =
        "Sort by: id, customer_name, created_at. Comma-separated, each optionally followed by "
        + ":asc (the default) or :desc. Default: created_at:desc.";

    [Fact]
    public async Task QueryParameter_WithSortableBy_DescribesSortingInItsCasing()
    {
        JsonElement document = await DocumentAsync(options => options.AddPaging());

        JsonElement sort = Parameter(document, "/orders", "get", "sort");
        Assert.Contains(SnakeCaseText, sort.GetProperty("description").GetString(), StringComparison.Ordinal);
        Assert.Equal("created_at:desc", sort.GetProperty("schema").GetProperty("default").GetString());
    }

    [Fact]
    public async Task RecordParameter_WithSortableBy_DescribesBodyPropertyAsDeclared()
    {
        JsonElement document = await DocumentAsync(options => options.AddPaging());

        JsonElement sort = document.GetProperty("components").GetProperty("schemas")
            .GetProperty("OrdersSearch").GetProperty("properties").GetProperty("sort");
        string expected =
            "Sort by: Id, CustomerName, CreatedAt. Comma-separated, each optionally followed by "
            + ":asc (the default) or :desc. Default: CreatedAt:desc.";
        Assert.Equal(
            expected,
            Assert.Single(sort.GetProperty("x-rules").EnumerateArray()).GetString());
        Assert.Equal("CreatedAt:desc", sort.GetProperty("default").GetString());
    }

    [Fact]
    public async Task QueryParameter_WithoutAddPaging_IsNotDescribed()
    {
        JsonElement document = await DocumentAsync(_ => { });

        JsonElement sort = Parameter(document, "/orders", "get", "sort");
        Assert.False(sort.TryGetProperty("description", out _));
    }

    private static JsonElement Parameter(JsonElement document, string path, string method, string name) =>
        document.GetProperty("paths").GetProperty(path).GetProperty(method)
            .GetProperty("parameters").EnumerateArray()
            .Single(parameter => parameter.GetProperty("name").GetString() == name);

    private static async Task<JsonElement> DocumentAsync(Action<OpenApiOptions> configure)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Services.AddOpenApi("v1", configure);
        await using WebApplication app = builder.Build();
        app.MapOpenApi();
        app.MapGet("/orders", ([AsParameters] OrdersQuery query) => Results.Ok(query.Sort));
        app.MapPost("/orders/search", (OrdersSearch search) => Results.Ok(search.Sort));
        await app.StartAsync(TestContext.Current.CancellationToken);

        string body = await app.GetTestClient().GetStringAsync(
            new Uri("/openapi/v1.json", UriKind.Relative),
            TestContext.Current.CancellationToken);
        await app.StopAsync(TestContext.Current.CancellationToken);
        using JsonDocument document = JsonDocument.Parse(body);
        return document.RootElement.Clone();
    }
}
