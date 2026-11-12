using System.Reflection;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Validation;

namespace Hlibz.OpenApi.Internal;

/// <summary>
/// Adds the <c>400</c> of <c>AddValidation()</c> to the operations it can answer with one. The
/// test is ASP.NET Core's own (<c>ValidationEndpointFilterFactory</c>): validation is registered,
/// the endpoint doesn't disable it, and a handler parameter that isn't a service is validatable;
/// plus one of its own, that the parameter has a rule to break (<see cref="ValidationRules"/>).
/// </summary>
/// <param name="responseType">The type of the response body.</param>
/// <param name="contentType">
/// The content type of the response body; <see langword="null"/> for the one the app answers with.
/// </param>
/// <param name="description">The response's description.</param>
internal sealed class ValidationProblemOperationTransformer(
    Type responseType,
    string? contentType,
    string description)
    : IOpenApiOperationTransformer
{
    /// <summary>The metadata key ASP.NET Core names a schema's component with.</summary>
    private const string SchemaIdKey = "x-schema-id";

    private const string StatusCode = "400";

    public async Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        if (operation.Responses?.ContainsKey(StatusCode) == true || !IsValidated(context))
        {
            return;
        }

        OpenApiSchema schema = await context
            .GetOrCreateSchemaAsync(responseType, parameterDescription: null, cancellationToken)
            .ConfigureAwait(false);

        OpenApiResponse response = new() { Description = description };
        response.Content = With(response.Content, contentType ?? AnsweredContentType(context), new OpenApiMediaType
        {
            Schema = Reference(schema, context.Document),
        });

        operation.Responses ??= [];
        operation.Responses[StatusCode] = response;
    }

    // ValidationOptions' lookups are experimental (ASP0029) on .NET 10 and stable on .NET 11. They
    // are what ASP.NET Core itself asks, so the answer here matches what it validates.
#pragma warning disable ASP0029
    private static bool IsValidated(OpenApiOperationTransformerContext context)
    {
        IList<object> metadata = context.Description.ActionDescriptor.EndpointMetadata;
        if (metadata.OfType<IDisableValidationMetadata>().Any()
            || metadata.OfType<MethodInfo>().FirstOrDefault() is not { } handler
            || context.ApplicationServices.GetService<IOptions<ValidationOptions>>()?.Value
                is not { Resolvers.Count: > 0 } options)
        {
            return false;
        }

        IServiceProviderIsService? services = context.ApplicationServices.GetService<IServiceProviderIsService>();
        return handler
            .GetParameters()
            .Any(parameter => !IsService(parameter, services)
                              && options.TryGetValidatableParameterInfo(parameter, out _)
                              && ValidationRules.Exist(parameter));
    }
#pragma warning restore ASP0029

    /// <summary>
    /// Gets the content type ASP.NET Core writes the <c>400</c> with: through an
    /// <c>IProblemDetailsService</c>, <c>application/problem+json</c>; without one, the filter
    /// returns the problem as a plain result, which is written as <c>application/json</c>.
    /// </summary>
    private static string AnsweredContentType(OpenApiOperationTransformerContext context) =>
        context.ApplicationServices.GetService<IProblemDetailsService>() is null
            ? "application/json"
            : "application/problem+json";

    private static bool IsService(ParameterInfo parameter, IServiceProviderIsService? services) =>
        parameter.GetCustomAttributes(inherit: true).Any(attribute =>
            attribute is IFromServiceMetadata or FromKeyedServicesAttribute)
        || services?.IsService(parameter.ParameterType) == true;

    /// <summary>
    /// Gets a <c>$ref</c> to <paramref name="schema"/> as a component, adding it when the
    /// document doesn't have it yet; the schema itself when it has no component name.
    /// </summary>
    private static IOpenApiSchema Reference(OpenApiSchema schema, OpenApiDocument? document)
    {
        if (document is null
            || schema.Metadata?.TryGetValue(SchemaIdKey, out object? value) != true
            || value is not string id)
        {
            return schema;
        }

        if (document.Components?.Schemas?.ContainsKey(id) != true)
        {
            document.AddComponent(id, schema);
        }

        return new OpenApiSchemaReference(id, document);
    }

    /// <summary>
    /// Puts <paramref name="value"/> into <paramref name="content"/>, creating it if needed.
    /// Generic so it compiles against Microsoft.OpenApi 2.x (<c>OpenApiMediaType</c> values) and
    /// 3.x (<c>IOpenApiMediaType</c> values).
    /// </summary>
    private static IDictionary<string, T> With<T>(IDictionary<string, T>? content, string key, T value)
    {
        IDictionary<string, T> result = content ?? new Dictionary<string, T>(StringComparer.Ordinal);
        result[key] = value;
        return result;
    }
}
