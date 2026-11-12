using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Hlibz.OpenApi.AspNetCore.Tests;

public sealed class ValidationProblemTests
{
    [Fact]
    public async Task AddValidationProblems_WithValidatedBody_AddsValidationProblem400()
    {
        JsonElement document = await StartAsync(options => options.AddValidationProblems());

        JsonElement response = Response400(document, "/signup", "post");
        Assert.Equal("The request didn't pass validation.", response.GetProperty("description").GetString());
        Assert.Equal(
            "#/components/schemas/HttpValidationProblemDetails",
            response.GetProperty("content").GetProperty("application/json")
                .GetProperty("schema").GetProperty("$ref").GetString());
        Assert.True(document.Schema("HttpValidationProblemDetails").GetProperty("properties").TryGetProperty("errors", out _));
    }

    [Theory]
    [InlineData("/query")]
    [InlineData("/paging")]
    public async Task AddValidationProblems_WithValidatedParameters_AddsValidationProblem400(string path)
    {
        JsonElement document = await StartAsync(options => options.AddValidationProblems());

        Assert.True(Has400(document, path, "get"));
    }

    [Theory]
    [InlineData("/plain")]
    [InlineData("/disabled")]
    [InlineData("/service")]
    [InlineData("/ids/{id}")]
    public async Task AddValidationProblems_WithNothingValidated_AddsNothing(string path)
    {
        JsonElement document = await StartAsync(options => options.AddValidationProblems());

        Assert.False(Has400(document, path, "get"));
    }

    [Fact]
    public async Task AddValidationProblems_WithDeclared400_LeavesIt()
    {
        JsonElement document = await StartAsync(options => options.AddValidationProblems());

        JsonElement response = Response400(document, "/declared", "post");
        Assert.Equal(
            "#/components/schemas/ProblemDetails",
            response.GetProperty("content").GetProperty("application/problem+json")
                .GetProperty("schema").GetProperty("$ref").GetString());
    }

    [Fact]
    public async Task AddValidationProblems_WithoutAddValidation_AddsNothing()
    {
        JsonElement document = await StartAsync(options => options.AddValidationProblems(), addValidation: false);

        Assert.False(Has400(document, "/signup", "post"));
        Assert.False(document.HasComponent("HttpValidationProblemDetails"));
    }

    [Fact]
    public async Task AddValidationProblems_WithOptions_UsesThem()
    {
        JsonElement document = await StartAsync(options => options.AddValidationProblems(problems =>
        {
            problems.ResponseType = typeof(ErrorResponse);
            problems.ContentType = "application/vnd.error+json";
            problems.Description = "Invalid input.";
        }));

        JsonElement response = Response400(document, "/signup", "post");
        Assert.Equal("Invalid input.", response.GetProperty("description").GetString());
        Assert.Equal(
            "#/components/schemas/ErrorResponse",
            response.GetProperty("content").GetProperty("application/vnd.error+json")
                .GetProperty("schema").GetProperty("$ref").GetString());
        Assert.True(document.HasComponent("ErrorResponse"));
    }

    [Theory]
    [InlineData(false, "application/json")]
    [InlineData(true, "application/problem+json")]
    public async Task AddValidationProblems_DocumentsWhatTheAppAnswers(bool problemDetails, string contentType)
    {
        HttpClient client = await StartClientAsync(options => options.AddValidationProblems(), problemDetails: problemDetails);
        JsonElement document = await client.GetDocumentAsync();

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            new Uri("/signup", UriKind.Relative),
            new { name = string.Empty },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(contentType, response.Content.Headers.ContentType?.MediaType);
        Assert.Equal([contentType], [.. Response400(document, "/signup", "post").GetProperty("content").EnumerateObject().Select(media => media.Name)]);
    }

    private static bool Has400(JsonElement document, string path, string method) =>
        document.Operation(path, method).GetProperty("responses").TryGetProperty("400", out _);

    private static JsonElement Response400(JsonElement document, string path, string method) =>
        document.Operation(path, method).GetProperty("responses").GetProperty("400");

    private static async Task<JsonElement> StartAsync(
        Action<Microsoft.AspNetCore.OpenApi.OpenApiOptions> configure,
        bool addValidation = true)
    {
        HttpClient client = await StartClientAsync(configure, addValidation);
        return await client.GetDocumentAsync();
    }

    private static Task<HttpClient> StartClientAsync(
        Action<Microsoft.AspNetCore.OpenApi.OpenApiOptions> configure,
        bool addValidation = true,
        bool problemDetails = false) =>
        TestApp.StartAsync(
            configure,
            app =>
            {
                app.MapPost("/signup", (Signup signup) => TypedResults.Ok(signup.Name));
                app.MapGet("/query", ([Range(1, 10)] int page) => TypedResults.Ok(page));
                app.MapGet("/paging", ([AsParameters] Paging paging) => TypedResults.Ok(paging.Limit));
                app.MapGet("/plain", (int page) => TypedResults.Ok(page));
                app.MapGet("/ids/{id}", (CountryId id) => TypedResults.Ok(id));
                app.MapGet("/disabled", ([Range(1, 10)] int page) => TypedResults.Ok(page)).DisableValidation();
                app.MapGet("/service", (Settings settings) => TypedResults.Ok(settings.PageSize));
                app.MapPost("/declared", (Signup signup) => TypedResults.Ok(signup.Name))
                    .ProducesProblem(StatusCodes.Status400BadRequest);
            },
            services =>
            {
                services.AddSingleton(new Settings());
                if (addValidation)
                {
                    TestApp.AddValidation(services);
                }

                if (problemDetails)
                {
                    services.AddProblemDetails();
                }
            });
}
