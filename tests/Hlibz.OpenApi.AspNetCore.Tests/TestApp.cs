using System.Text.Json;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Hlibz.OpenApi.AspNetCore.Tests;

/// <summary>
/// Starts an in-memory app with authentication and authorization, maps its endpoints and
/// reads back its OpenAPI documents.
/// </summary>
internal static class TestApp
{
    /// <summary>Starts an app with one document, <c>v1</c>, configured by <paramref name="configure"/>.</summary>
    public static Task<HttpClient> StartAsync(
        Action<OpenApiOptions> configure,
        Action<WebApplication> map,
        Action<IServiceCollection>? services = null) =>
        StartAsync(
            serviceCollection =>
            {
                serviceCollection.AddOpenApi("v1", configure);
                services?.Invoke(serviceCollection);
            },
            map);

    /// <summary>Starts an app that registers its own documents.</summary>
    public static async Task<HttpClient> StartAsync(
        Action<IServiceCollection> services,
        Action<WebApplication> map)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Services.AddAuthentication("Test").AddScheme<AuthenticationSchemeOptions, NoAuthHandler>("Test", _ => { });
        builder.Services.AddAuthorization();
        services(builder.Services);

        WebApplication app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapOpenApi().AllowAnonymous();
        map(app);

        await app.StartAsync(TestContext.Current.CancellationToken);
        return app.GetTestClient();
    }

    /// <summary>
    /// The project's only <c>AddValidation()</c> call: on .NET 10 the validation source generator
    /// fails when a project calls it more than once ("hintName ... must be unique").
    /// </summary>
    public static void AddValidation(IServiceCollection services) => services.AddValidation();

    public static async Task<JsonElement> GetDocumentAsync(this HttpClient client, string name = "v1")
    {
        string body = await client.GetStringAsync(
            new Uri($"/openapi/{name}.json", UriKind.Relative),
            TestContext.Current.CancellationToken);
        using JsonDocument document = JsonDocument.Parse(body);
        return document.RootElement.Clone();
    }

    public static JsonElement Schema(this JsonElement document, string name) =>
        document.GetProperty("components").GetProperty("schemas").GetProperty(name);

    public static JsonElement Operation(this JsonElement document, string path, string method = "get") =>
        document.GetProperty("paths").GetProperty(path).GetProperty(method);

    public static JsonElement Parameter(this JsonElement operation, string name) =>
        operation.GetProperty("parameters").EnumerateArray()
            .Single(parameter => parameter.GetProperty("name").GetString() == name);

    public static bool HasComponent(this JsonElement document, string name) =>
        document.TryGetProperty("components", out JsonElement components)
        && components.TryGetProperty("schemas", out JsonElement schemas)
        && schemas.TryGetProperty(name, out _);

    public static string[] Strings(this JsonElement array) =>
        [.. array.EnumerateArray().Select(item => item.GetString()!)];
}
