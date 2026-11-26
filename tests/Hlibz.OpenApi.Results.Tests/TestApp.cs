using System.Text.Json;

using Hlibz.Results.AspNetCore;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Hlibz.OpenApi.Results.Tests;

/// <summary>
/// Starts an in-memory app with result responses, maps its endpoints and reads back its OpenAPI
/// document.
/// </summary>
internal static class TestApp
{
    public static async Task<JsonElement> DocumentAsync(
        Action<OpenApiOptions> configure,
        Action<IEndpointRouteBuilder> map,
        Action<IServiceCollection>? services = null)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Services.AddResultErrorResponses();
        builder.Services.AddOpenApi("v1", configure);
        services?.Invoke(builder.Services);
        await using WebApplication app = builder.Build();
        app.MapOpenApi();
        map(app);
        await app.StartAsync(TestContext.Current.CancellationToken);

        string body = await app.GetTestClient().GetStringAsync(
            new Uri("/openapi/v1.json", UriKind.Relative),
            TestContext.Current.CancellationToken);
        await app.StopAsync(TestContext.Current.CancellationToken);
        using JsonDocument document = JsonDocument.Parse(body);
        return document.RootElement.Clone();
    }

    /// <summary>Maps <c>GET orders/{id}</c> and <c>DELETE orders/{id}</c> under <c>WithResultResponses()</c>.</summary>
    public static void MapOrders(IEndpointRouteBuilder app)
    {
        RouteGroupBuilder orders = app.MapGroup("orders").WithResultResponses();
        orders.MapGet("{id:int}", Result<OrderDto, GetOrderError> (int id) => new OrderNotFound(id));
        orders.MapDelete("{id:int}", Result<Unit, CancelOrderError> (int id) => new OrderClosed());
    }

    public static JsonElement Schema(this JsonElement document, string name) =>
        document.GetProperty("components").GetProperty("schemas").GetProperty(name);

    public static bool HasSchema(this JsonElement document, string name) =>
        document.GetProperty("components").GetProperty("schemas").TryGetProperty(name, out _);

    public static JsonElement Operation(this JsonElement document, string path, string method) =>
        document.GetProperty("paths").GetProperty(path).GetProperty(method);

    /// <summary>Gets the <c>application/json</c> schema of <paramref name="operation"/>'s <paramref name="status"/> response.</summary>
    public static JsonElement Body(this JsonElement operation, int status) =>
        operation.GetProperty("responses")
            .GetProperty(status.ToString(System.Globalization.CultureInfo.InvariantCulture))
            .GetProperty("content")
            .GetProperty("application/json")
            .GetProperty("schema");

    public static string Ref(this JsonElement schema) => schema.GetProperty("$ref").GetString()!;

    public static string[] Strings(this JsonElement array) =>
        [.. array.EnumerateArray().Select(item => item.GetString()!)];

    public static string Component(string name) => $"#/components/schemas/{name}";
}
