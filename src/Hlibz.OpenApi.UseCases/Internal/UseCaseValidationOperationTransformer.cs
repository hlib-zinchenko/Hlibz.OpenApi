using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Hlibz.OpenApi.UseCases.Internal;

/// <summary>
/// Adds the <c>400</c> an invalid use case is answered with to every operation whose handler
/// takes the handler of a use case that can fail validation (as <see cref="IUseCaseCatalog"/>
/// says), unless the operation already documents a <c>400</c>.
/// </summary>
/// <param name="responseType">The type of the response body.</param>
/// <param name="contentType">
/// The content type of the response body; <see langword="null"/> for the one the app answers with.
/// </param>
/// <param name="description">The response's description.</param>
internal sealed class UseCaseValidationOperationTransformer(
    Type responseType,
    string? contentType,
    string description)
    : IOpenApiOperationTransformer
{
    private const string SchemaIdKey = "x-schema-id";
    private const string StatusCode = "400";

    public async Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        if (operation.Responses?.ContainsKey(StatusCode) == true || !CanFailValidation(context))
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

    private static bool CanFailValidation(OpenApiOperationTransformerContext context)
    {
        if (context.Description.ActionDescriptor.EndpointMetadata.OfType<MethodInfo>().FirstOrDefault()
                is not { } handler
            || context.ApplicationServices.GetService<IUseCaseCatalog>() is not { } catalog)
        {
            return false;
        }

        UseCaseDescriptor[] validated = [.. catalog.UseCases.Where(useCase => useCase.CanFailValidation)];
        return validated.Length > 0
               && handler.GetParameters().Any(parameter => Handles(parameter.ParameterType, validated));
    }

    /// <summary>
    /// Whether a handler parameter is the handler of one of <paramref name="useCases"/>: one of its
    /// handler interfaces (its own, or the closed <c>IUseCaseHandler&lt;...&gt;</c>), each of
    /// which belongs to exactly one handler, since a handler handles exactly one use case.
    /// </summary>
    private static bool Handles(Type parameterType, UseCaseDescriptor[] useCases) =>
        parameterType != typeof(IUseCaseHandler)
        && typeof(IUseCaseHandler).IsAssignableFrom(parameterType)
        && useCases.Any(useCase => parameterType.IsAssignableFrom(useCase.HandlerType));

    private static string AnsweredContentType(OpenApiOperationTransformerContext context) =>
        context.ApplicationServices.GetService<IProblemDetailsService>() is null
            ? "application/json"
            : "application/problem+json";

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
    /// Generic so it compiles against Microsoft.OpenApi 2.x and 3.x.
    /// </summary>
    private static IDictionary<string, T> With<T>(IDictionary<string, T>? content, string key, T value)
    {
        IDictionary<string, T> result = content ?? new Dictionary<string, T>(StringComparer.Ordinal);
        result[key] = value;
        return result;
    }
}
