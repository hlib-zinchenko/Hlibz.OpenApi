using System.Text.Json;

using Hlibz.OpenApi;
using Hlibz.Results.AspNetCore;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Hlibz.OpenApi.UseCases.Tests;

public sealed class UseCaseOpenApiTests
{
    [Fact]
    public async Task BodyProperty_WithDescribedRuleOnRecordParameter_WritesKeywordAndText()
    {
        JsonElement document = await TestApp.DocumentAsync(options => options.AddUseCases(), MapSignUp);

        JsonElement country = document.Schema("SignUpCommand").Property("country");
        Assert.Equal("^[A-Z]{2}$", country.GetProperty("pattern").GetString());
        Assert.Equal([CountryCodeAttribute.Text], country.GetProperty("x-rules").Strings());
        Assert.Contains(CountryCodeAttribute.Text, country.GetProperty("description").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task BodyProperty_WithCollectionAndIdRules_WritesTheirKeywords()
    {
        JsonElement document = await TestApp.DocumentAsync(options => options.AddUseCases(), MapSignUp);
        JsonElement schema = document.Schema("SignUpCommand");

        JsonElement tags = schema.Property("tags");
        Assert.True(tags.GetProperty("uniqueItems").GetBoolean());
        Assert.Equal(10, tags.GetProperty("maxItems").GetInt32());
        JsonElement referrer = schema.Property("referrer");
        Assert.Equal(
            Guid.Empty.ToString(),
            referrer.GetProperty("not").GetProperty("const").GetString());
    }

    [Fact]
    public async Task UseCaseType_WithRuleSummary_DescribesTypeSchema()
    {
        JsonElement document = await TestApp.DocumentAsync(options => options.AddUseCases(), MapSignUp);

        Assert.Equal(
            ["A user signs up once per email."],
            document.Schema("SignUpCommand").GetProperty("x-rules").Strings());
    }

    [Fact]
    public async Task EnumProperty_WithRuleSummary_PutsTextNextToRefOnly()
    {
        JsonElement document = await TestApp.DocumentAsync(options => options.AddUseCases(), MapSignUp);

        JsonElement plan = document.Schema("SignUpCommand").Property("plan");
        Assert.True(plan.TryGetProperty("$ref", out _));

        // After the summary Microsoft.AspNetCore.OpenApi's own XML comment generator put there.
        Assert.EndsWith(
            "\n\nBusiness plans need a sales call first.",
            plan.GetProperty("description").GetString(),
            StringComparison.Ordinal);
        Assert.False(document.Schema("Plan").TryGetProperty("x-rules", out _));
    }

    [Fact]
    public async Task Rules_WithXmlDocumentation_KeepXmlSummaryThenRuleSummary()
    {
        JsonElement document = await TestApp.DocumentAsync(
            options => options.AddXmlDocumentation().AddUseCases(),
            MapSignUp);
        JsonElement schema = document.Schema("SignUpCommand");

        Assert.Equal(
            $"The user's country.\n\n{CountryCodeAttribute.Text}",
            schema.Property("country").GetProperty("description").GetString());
        Assert.Equal(
            "The plan the user starts on.\n\nBusiness plans need a sales call first.",
            schema.Property("plan").GetProperty("description").GetString());
        Assert.Equal(
            "Signs a user up.\n\nA user signs up once per email.",
            schema.GetProperty("description").GetString());
    }

    [Fact]
    public async Task Parameter_WithDescribedRule_WritesKeywordsOnItsSchema()
    {
        JsonElement document = await TestApp.DocumentAsync(
            options => options.AddUseCases(),
            app => app.MapGet("discounts", ([Percent] decimal percent) => percent));

        JsonElement schema = document.Operation("/discounts", "get").Parameter("percent").GetProperty("schema");
        Assert.Equal(0, schema.GetProperty("exclusiveMinimum").GetDecimal());
        Assert.Equal(100, schema.GetProperty("maximum").GetDecimal());
    }

    [Fact]
    public async Task Operation_WithHandlerOfValidatedUseCase_Documents400()
    {
        JsonElement document = await TestApp.DocumentAsync(options => options.AddUseCases(), MapSignUp);

        JsonElement response = document.Operation("/sign-up", "post").GetProperty("responses").GetProperty("400");
        Assert.Equal("The request didn't pass validation.", response.GetProperty("description").GetString());
        Assert.Equal(
            "#/components/schemas/HttpValidationProblemDetails",
            response.GetProperty("content").GetProperty("application/json").GetProperty("schema").GetProperty("$ref").GetString());
    }

    [Fact]
    public async Task Operation_WithClosedHandlerInterface_Documents400()
    {
        JsonElement document = await TestApp.DocumentAsync(
            options => options.AddUseCases(),
            app => app.MapPost(
                "sign-up-closed",
                async (SignUpCommand command, [FromServices] IUseCaseHandler<SignUpCommand, AccountDto> handler) =>
                    (await handler.HandleAsync(command)).ToHttpResult()));

        Assert.True(document.Operation("/sign-up-closed", "post").GetProperty("responses").TryGetProperty("400", out _));
    }

    [Fact]
    public async Task Operation_WithHandlerDeclaringErrors_Documents400()
    {
        JsonElement document = await TestApp.DocumentAsync(
            options => options.AddUseCases(),
            app => app.MapPut(
                "email",
                async (ChangeEmailCommand command, [FromServices] IChangeEmailCommandHandler handler) =>
                    (await handler.HandleAsync(command)).ToHttpResult()));

        Assert.True(document.Operation("/email", "put").GetProperty("responses").TryGetProperty("400", out _));
    }

    [Fact]
    public async Task Operation_WithHandlerOfUseCaseWithoutRules_DocumentsNo400()
    {
        JsonElement document = await TestApp.DocumentAsync(
            options => options.AddUseCases(),
            app => app.MapGet(
                "hello",
                async (string? name, [FromServices] IUseCaseHandler<HelloQuery, string> handler) =>
                    (await handler.HandleAsync(new HelloQuery(name))).ToHttpResult()));

        Assert.False(document.Operation("/hello", "get").GetProperty("responses").TryGetProperty("400", out _));
    }

    [Fact]
    public async Task Operation_WithDeclared400_KeepsIt()
    {
        JsonElement document = await TestApp.DocumentAsync(
            options => options.AddUseCases(),
            app => MapSignUp((IEndpointRouteBuilder)app).ProducesProblem(StatusCodes.Status400BadRequest));

        JsonElement content = document.Operation("/sign-up", "post").GetProperty("responses").GetProperty("400").GetProperty("content");
        Assert.True(content.TryGetProperty("application/problem+json", out _));
        Assert.False(content.TryGetProperty("application/json", out _));
    }

    [Fact]
    public async Task Operation_WithConfiguredResponse_DocumentsThatBody()
    {
        JsonElement document = await TestApp.DocumentAsync(
            options => options.AddUseCases(useCases =>
            {
                useCases.ValidationResponseType = typeof(ErrorResponse);
                useCases.ValidationDescription = "Check the fields.";
            }),
            MapSignUp);

        JsonElement response = document.Operation("/sign-up", "post").GetProperty("responses").GetProperty("400");
        Assert.Equal("Check the fields.", response.GetProperty("description").GetString());
        Assert.Equal(
            "#/components/schemas/ErrorResponse",
            response.GetProperty("content").GetProperty("application/json").GetProperty("schema").GetProperty("$ref").GetString());
    }

    [Fact]
    public async Task RecordParameters_WithDataAnnotations_WriteTheirKeywords()
    {
        JsonElement document = await TestApp.DocumentAsync(
            options => options.AddUseCases(),
            app => app.MapPost(
                "orders",
                async (OrderCommand command, [FromServices] IUseCaseHandler<OrderCommand> handler) =>
                    (await handler.HandleAsync(command)).ToHttpResult()));
        JsonElement schema = document.Schema("OrderCommand");

        Assert.Equal(1, schema.Property("seats").GetProperty("minimum").GetInt32());
        Assert.Equal(5, schema.Property("seats").GetProperty("maximum").GetInt32());
        Assert.Equal(2, schema.Property("note").GetProperty("minLength").GetInt32());
        Assert.Equal(20, schema.Property("note").GetProperty("maxLength").GetInt32());
        Assert.Equal(1, schema.Property("lines").GetProperty("minItems").GetInt32());
        Assert.Equal(3, schema.Property("lines").GetProperty("maxItems").GetInt32());
        Assert.Equal(0, schema.Property("share").GetProperty("exclusiveMinimum").GetInt32());
        Assert.Equal(1, schema.Property("share").GetProperty("maximum").GetInt32());
        Assert.Equal("^[a-z]+$", schema.Property("slug").GetProperty("pattern").GetString());
        Assert.Equal("uri", schema.Property("site").GetProperty("format").GetString());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ComponentTypedProperty_WithBothRuleKinds_KeepsBothSentencesInEitherOrder(bool useCasesFirst)
    {
        JsonElement document = await TestApp.DocumentAsync(
            options => _ = useCasesFirst
                ? options.AddUseCases().AddOpenApiRules()
                : options.AddOpenApiRules().AddUseCases(),
            app => app.MapPost(
                "plan",
                async (ChangePlanCommand command, [FromServices] IUseCaseHandler<ChangePlanCommand> handler) =>
                    (await handler.HandleAsync(command)).ToHttpResult()));

        string description = document.Schema("ChangePlanCommand").Property("to").GetProperty("description").GetString()!;
        Assert.Contains("Plan changes are logged.", description, StringComparison.Ordinal);
        Assert.Contains("Only to a plan you can pay for.", description, StringComparison.Ordinal);
    }

    private static void MapSignUp(WebApplication app) => _ = MapSignUp((IEndpointRouteBuilder)app);

    private static RouteHandlerBuilder MapSignUp(IEndpointRouteBuilder app) =>
        app.MapPost(
            "sign-up",
            async (SignUpCommand command, [FromServices] ISignUpCommandHandler handler) =>
                (await handler.HandleAsync(command)).ToHttpResult());
}
