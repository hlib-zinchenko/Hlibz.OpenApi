using System.Text.Json;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Hlibz.OpenApi.UseCases.Tests;

/// <summary>
/// Starts an in-memory app with this assembly's use cases, maps its endpoints and reads back its
/// OpenAPI document.
/// </summary>
internal static class TestApp
{
    public static async Task<JsonElement> DocumentAsync(
        Action<OpenApiOptions> configure,
        Action<WebApplication> map,
        Action<IServiceCollection>? services = null)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Services.AddUseCases([typeof(TestApp).Assembly]);
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

    public static JsonElement Schema(this JsonElement document, string name) =>
        document.GetProperty("components").GetProperty("schemas").GetProperty(name);

    public static JsonElement Property(this JsonElement schema, string name) =>
        schema.GetProperty("properties").GetProperty(name);

    public static JsonElement Operation(this JsonElement document, string path, string method) =>
        document.GetProperty("paths").GetProperty(path).GetProperty(method);

    public static JsonElement Parameter(this JsonElement operation, string name) =>
        operation.GetProperty("parameters").EnumerateArray()
            .Single(parameter => parameter.GetProperty("name").GetString() == name);

    public static string[] Strings(this JsonElement array) =>
        [.. array.EnumerateArray().Select(item => item.GetString()!)];
}
