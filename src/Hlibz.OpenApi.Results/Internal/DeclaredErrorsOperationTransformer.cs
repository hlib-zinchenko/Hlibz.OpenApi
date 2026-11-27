using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Hlibz.OpenApi.Results.Internal;

/// <summary>
/// Replaces the error body Hlibz.Results documents per status code (a <c>$ref</c> to
/// <see cref="ErrorResponse"/> or <see cref="ResultEnvelope"/>) with each declared error's own
/// body: a component that narrows that body's <c>code</c> to the error's and its
/// <c>details</c> to the error's schema. Several errors on one status code are a <c>oneOf</c>
/// with a discriminator on <c>code</c>.
/// </summary>
/// <remarks>
/// A <see cref="Result{T, TError}"/> accepts any <see cref="AmbientError"/> whatever its error
/// set, so a status code also lists the ambient errors the app sends with it
/// (<see cref="ResultHttpOptions.StatusCodes"/>): narrowing a <c>400</c> to the declared errors
/// alone would hide <c>validation_failed</c>, and a <c>403</c> the authorization gate's
/// <c>forbidden</c>. A status code no error declares is left alone.
/// </remarks>
internal sealed class DeclaredErrorsOperationTransformer : IOpenApiOperationTransformer
{
    /// <summary>The ambient errors, in code order.</summary>
    private static readonly ErrorDescriptor[] AmbientErrors =
    [
        .. new ErrorCodeCatalog([]).All
            .Where(error => typeof(AmbientError).IsAssignableFrom(error.Type)),
    ];

    public async Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        // An endpoint can declare several sets: its handler's Result<T, TError> and ProducesErrorSet<T>().
        DeclaredErrorResponse[] declared =
        [
            .. context.Description.ActionDescriptor.EndpointMetadata
                .OfType<DeclaredErrorsMetadata>()
                .SelectMany(metadata => metadata.Errors)
                .DistinctBy(error => (error.Error.Type, error.StatusCode)),
        ];
        if (operation.Responses is null || context.Document is not { } document || declared.Length == 0)
        {
            return;
        }

        ResultHttpOptions options = context.ApplicationServices
            .GetService<IOptions<ResultHttpOptions>>()?.Value ?? new ResultHttpOptions();
        foreach (IGrouping<int, DeclaredErrorResponse> status in declared
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

            ErrorDescriptor[] declaredErrors = [.. status.Select(response => response.Error)];
            List<(string Code, OpenApiSchemaReference Schema)> errors = [];
            foreach (ErrorDescriptor error in declaredErrors.Concat(
                         AmbientErrorsSentWith(status.Key, declaredErrors, options)))
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
    /// Gets the ambient errors the app sends with <paramref name="statusCode"/> that
    /// <paramref name="declared"/> doesn't list already.
    /// </summary>
    private static IEnumerable<ErrorDescriptor> AmbientErrorsSentWith(
        int statusCode,
        ErrorDescriptor[] declared,
        ResultHttpOptions options) =>
        AmbientErrors.Where(ambient =>
            options.StatusCodes.TryGetValue(ambient.Status, out int sent)
            && sent == statusCode
            && !declared.Any(error => error.Type == ambient.Type));

    /// <summary>
    /// Gets the component of <paramref name="error"/>'s body: the base body narrowed to its code
    /// and, when it carries data, to its details (a <see cref="ValidationFailed"/>'s go out as the
    /// validation errors instead). Added to the document the first time.
    /// </summary>
    private static async Task<OpenApiSchemaReference> ErrorBodyAsync(
        ErrorDescriptor error,
        Component body,
        OpenApiDocument document,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        bool validation = error.Type == typeof(ValidationFailed);
        Component? details = error.HasDetails && !validation
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

        if (validation)
        {
            own.Required.Add(PropertyName(body.Schema, nameof(ErrorResponse.ValidationErrors)));
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
            || schema.Metadata?.TryGetValue(SchemaReferences.SchemaIdKey, out object? value) != true
            || value is not string id)
        {
            return null;
        }

        if (document.Components?.Schemas?.ContainsKey(id) != true)
        {
            document.AddComponent(id, schema);
            SchemaReferences.Resolve(schema, document);
        }

        return new Component(id, schema);
    }

    /// <summary>
    /// Gets the JSON name of <paramref name="property"/> in <paramref name="schema"/>, as the app's
    /// naming policy wrote it: the same letters in any case, with or without the <c>_</c> or
    /// <c>-</c> of the snake and kebab case policies (<c>ValidationErrors</c> is
    /// <c>validation_errors</c>).
    /// </summary>
    private static string PropertyName(OpenApiSchema schema, string property) =>
        schema.Properties?.Keys.FirstOrDefault(name => SameWords(name, property))
        ?? JsonNamingPolicy.CamelCase.ConvertName(property);

    private static bool SameWords(string name, string property) =>
        name.Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant)
            .SequenceEqual(property.Select(char.ToUpperInvariant));

    /// <summary>A schema and the name of its component.</summary>
    private sealed record Component(string Id, OpenApiSchema Schema);
}
