using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Hlibz.OpenApi.Results.Internal;

/// <summary>
/// Replaces the error body Hlibz.Results documents per status code (a <c>$ref</c> to
/// <see cref="ErrorResponse"/> or <see cref="ResultEnvelope"/>) with each declared error's own
/// body: a component that narrows that body's <c>code</c> to the error's and its
/// <c>details</c> to the error's schema. Several errors on one status code are a <c>oneOf</c>
/// with a discriminator on <c>code</c>.
/// </summary>
internal sealed class DeclaredErrorsOperationTransformer : IOpenApiOperationTransformer
{
    /// <summary>The metadata key ASP.NET Core names a schema's component with.</summary>
    private const string SchemaIdKey = "x-schema-id";

    public async Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        if (operation.Responses is null
            || context.Document is not { } document
            || context.Description.ActionDescriptor.EndpointMetadata
                .OfType<DeclaredErrorsMetadata>()
                .LastOrDefault() is not { } declared)
        {
            return;
        }

        foreach (IGrouping<int, DeclaredErrorResponse> status in declared.Errors
            .Where(error => error.ResponseType is not null)
            .GroupBy(error => error.StatusCode))
        {
            string key = status.Key.ToString(CultureInfo.InvariantCulture);
            if (!operation.Responses.TryGetValue(key, out IOpenApiResponse? response)
                || response.Content is null)
            {
                continue;
            }

            Component? body = await ComponentAsync(status.First().ResponseType!, context, cancellationToken)
                .ConfigureAwait(false);

            // Only the body Hlibz.Results documented: a response the endpoint declares itself wins.
            OpenApiMediaType[] media =
            [
                .. response.Content.Values
                    .OfType<OpenApiMediaType>()
                    .Where(item => item.Schema is OpenApiSchemaReference reference
                                   && reference.Reference.Id == body?.Id),
            ];
            if (body is null || media.Length == 0)
            {
                continue;
            }

            List<(string Code, OpenApiSchemaReference Schema)> errors = [];
            foreach (ErrorDescriptor error in status.Select(response => response.Error))
            {
                errors.Add((
                    error.Code,
                    await ErrorBodyAsync(error, body, document, context, cancellationToken)
                        .ConfigureAwait(false)));
            }

            IOpenApiSchema schema = errors.Count == 1
                ? errors[0].Schema
                : new OpenApiSchema
                {
                    OneOf = [.. errors.Select(error => (IOpenApiSchema)error.Schema)],
                    Discriminator = new OpenApiDiscriminator
                    {
                        PropertyName = PropertyName(body.Schema, nameof(ErrorResponse.Code)),
                        Mapping = errors.ToDictionary(
                            error => error.Code,
                            error => error.Schema,
                            StringComparer.Ordinal),
                    },
                };

            foreach (OpenApiMediaType item in media)
            {
                item.Schema = schema;
            }
        }
    }

    /// <summary>
    /// Gets the component of <paramref name="error"/>'s body: the base body narrowed to its code
    /// and, when it carries data, to its details. Added to the document the first time.
    /// </summary>
    private static async Task<OpenApiSchemaReference> ErrorBodyAsync(
        ErrorDescriptor error,
        Component body,
        OpenApiDocument document,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        Component? details = error.HasDetails
            ? await ComponentAsync(error.Type, context, cancellationToken).ConfigureAwait(false)
            : null;
        string id = (details?.Id ?? error.Type.Name) + body.Id;
        if (document.Components?.Schemas?.ContainsKey(id) == true)
        {
            return new OpenApiSchemaReference(id, document);
        }

        string code = PropertyName(body.Schema, nameof(ErrorResponse.Code));
        OpenApiSchema own = new()
        {
            Type = JsonSchemaType.Object,
            Required = new HashSet<string>(StringComparer.Ordinal) { code },
            Properties = new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal)
            {
                [code] = new OpenApiSchema
                {
                    Type = JsonSchemaType.String,
                    Enum = [JsonValue.Create(error.Code)],
                },
            },
        };
        if (details is not null)
        {
            string name = PropertyName(body.Schema, nameof(ErrorResponse.Details));
            own.Required.Add(name);
            own.Properties[name] = new OpenApiSchemaReference(details.Id, document);
        }

        document.AddComponent(id, new OpenApiSchema
        {
            Description = error.Message,
            AllOf = [new OpenApiSchemaReference(body.Id, document), own],
        });
        return new OpenApiSchemaReference(id, document);
    }

    /// <summary>
    /// Gets the component <paramref name="type"/>'s schema is, adding it to the document when it
    /// isn't there yet; <see langword="null"/> when its schema has no component name.
    /// </summary>
    private static async Task<Component?> ComponentAsync(
        Type type,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        OpenApiSchema schema = await context
            .GetOrCreateSchemaAsync(type, parameterDescription: null, cancellationToken)
            .ConfigureAwait(false);
        if (context.Document is not { } document
            || schema.Metadata?.TryGetValue(SchemaIdKey, out object? value) != true
            || value is not string id)
        {
            return null;
        }

        if (document.Components?.Schemas?.ContainsKey(id) != true)
        {
            document.AddComponent(id, schema);
        }

        return new Component(id, schema);
    }

    /// <summary>
    /// Gets the JSON name of <paramref name="property"/> in <paramref name="schema"/>, as the app's
    /// naming policy wrote it.
    /// </summary>
    private static string PropertyName(OpenApiSchema schema, string property) =>
        schema.Properties?.Keys.FirstOrDefault(name =>
            string.Equals(name, property, StringComparison.OrdinalIgnoreCase))
        ?? JsonNamingPolicy.CamelCase.ConvertName(property);

    /// <summary>A schema and the name of its component.</summary>
    private sealed record Component(string Id, OpenApiSchema Schema);
}
