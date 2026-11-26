using System.Text.Json;

using Hlibz.Results.AspNetCore;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace Hlibz.OpenApi.Results.Tests;

public sealed class DeclaredErrorsTests
{
    [Fact]
    public async Task Status_WithOneDeclaredError_RefersToThatErrorsBody()
    {
        JsonElement document = await TestApp.DocumentAsync(options => options.AddResults(), TestApp.MapOrders);

        Assert.Equal(
            TestApp.Component("OrderNotFoundErrorResponse"),
            document.Operation("/orders/{id}", "get").Body(404).Ref());
    }

    [Fact]
    public async Task ErrorBody_WithDetails_NarrowsCodeAndDetails()
    {
        JsonElement document = await TestApp.DocumentAsync(options => options.AddResults(), TestApp.MapOrders);

        JsonElement body = document.Schema("OrderNotFoundErrorResponse");
        Assert.Equal("The order wasn't found.", body.GetProperty("description").GetString());
        JsonElement[] allOf = [.. body.GetProperty("allOf").EnumerateArray()];
        Assert.Equal(TestApp.Component(nameof(ErrorResponse)), allOf[0].Ref());
        Assert.Equal(["code", "details"], allOf[1].GetProperty("required").Strings().Order(StringComparer.Ordinal));
        JsonElement properties = allOf[1].GetProperty("properties");
        Assert.Equal(["orders_1001"], properties.GetProperty("code").GetProperty("enum").Strings());
        Assert.Equal(TestApp.Component(nameof(OrderNotFound)), properties.GetProperty("details").Ref());
        Assert.True(document.Schema(nameof(OrderNotFound)).GetProperty("properties").TryGetProperty("orderId", out _));
    }

    [Fact]
    public async Task ErrorBody_WithoutDetails_NarrowsCodeOnly()
    {
        JsonElement document = await TestApp.DocumentAsync(options => options.AddResults(), TestApp.MapOrders);

        JsonElement own = document.Schema("OrderClosedErrorResponse").GetProperty("allOf")[1];
        Assert.Equal(["code"], own.GetProperty("required").Strings());
        Assert.False(own.GetProperty("properties").TryGetProperty("details", out _));
        Assert.False(document.HasSchema(nameof(OrderClosed)));
    }

    [Fact]
    public async Task Status_WithSeveralDeclaredErrors_IsOneOfThemByCode()
    {
        JsonElement document = await TestApp.DocumentAsync(options => options.AddResults(), TestApp.MapOrders);

        JsonElement body = document.Operation("/orders/{id}", "delete").Body(409);
        Assert.Equal(
            [TestApp.Component("OrderClosedErrorResponse"), TestApp.Component("OrderPaidErrorResponse")],
            body.GetProperty("oneOf").EnumerateArray().Select(schema => schema.Ref()));
        JsonElement discriminator = body.GetProperty("discriminator");
        Assert.Equal("code", discriminator.GetProperty("propertyName").GetString());
        JsonElement mapping = discriminator.GetProperty("mapping");
        Assert.Equal(TestApp.Component("OrderClosedErrorResponse"), mapping.GetProperty("orders_1002").GetString());
        Assert.Equal(TestApp.Component("OrderPaidErrorResponse"), mapping.GetProperty("orders_1003").GetString());
    }

    [Fact]
    public async Task ErrorBody_DeclaredByTwoOperations_IsOneComponent()
    {
        JsonElement document = await TestApp.DocumentAsync(options => options.AddResults(), TestApp.MapOrders);

        Assert.Equal(
            TestApp.Component("OrderNotFoundErrorResponse"),
            document.Operation("/orders/{id}", "delete").Body(404).Ref());
    }

    [Fact]
    public async Task Status_WithoutAddResults_KeepsTheErrorResponse()
    {
        JsonElement document = await TestApp.DocumentAsync(_ => { }, TestApp.MapOrders);

        Assert.Equal(
            TestApp.Component(nameof(ErrorResponse)),
            document.Operation("/orders/{id}", "delete").Body(409).Ref());
        Assert.False(document.HasSchema("OrderClosedErrorResponse"));
    }

    [Fact]
    public async Task Status_TheEndpointDocumentsItself_IsLeftAsItIs()
    {
        JsonElement document = await TestApp.DocumentAsync(
            options => options.AddResults(),
            app => app.MapGroup("orders").WithResultResponses()
                .MapDelete("{id:int}", Result<Unit, CancelOrderError> (int id) => new OrderClosed())
                .Produces<ProblemDetails>(StatusCodes.Status409Conflict));

        JsonElement operation = document.Operation("/orders/{id}", "delete");
        Assert.Equal(TestApp.Component(nameof(ProblemDetails)), operation.Body(409).Ref());
        Assert.Equal(TestApp.Component("OrderNotFoundErrorResponse"), operation.Body(404).Ref());
    }

    [Fact]
    public async Task ErrorBody_InAnEnvelope_NarrowsTheEnvelope()
    {
        JsonElement document = await TestApp.DocumentAsync(
            options => options.AddResults(),
            app => app.MapGroup("orders").WithResultEnvelopes()
                .MapGet("{id:int}", Result<OrderDto, GetOrderError> (int id) => new OrderNotFound(id)));

        Assert.Equal(
            TestApp.Component("OrderNotFoundResultEnvelope"),
            document.Operation("/orders/{id}", "get").Body(404).Ref());
        JsonElement allOf = document.Schema("OrderNotFoundResultEnvelope").GetProperty("allOf");
        Assert.Equal(TestApp.Component(nameof(ResultEnvelope)), allOf[0].Ref());
        Assert.Equal(TestApp.Component(nameof(OrderNotFound)), allOf[1].GetProperty("properties").GetProperty("details").Ref());
    }

    [Fact]
    public async Task ErrorBody_WithoutNamingPolicy_UsesTheBodysPropertyNames()
    {
        JsonElement document = await TestApp.DocumentAsync(
            options => options.AddResults(),
            TestApp.MapOrders,
            services => services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(json => json.SerializerOptions.PropertyNamingPolicy = null));

        JsonElement properties = document.Schema("OrderNotFoundErrorResponse").GetProperty("allOf")[1].GetProperty("properties");
        Assert.True(properties.TryGetProperty("Code", out _));
        Assert.True(properties.TryGetProperty("Details", out _));
        Assert.Equal(
            "Code",
            document.Operation("/orders/{id}", "delete").Body(409).GetProperty("discriminator").GetProperty("propertyName").GetString());
    }

    [Fact]
    public async Task Ambient_Errors_AreNotDocumentedPerOperation()
    {
        JsonElement document = await TestApp.DocumentAsync(options => options.AddResults(), TestApp.MapOrders);

        Assert.False(document.Operation("/orders/{id}", "get").GetProperty("responses").TryGetProperty("400", out _));
    }
}
