namespace Hlibz.OpenApi.Internal;

/// <summary>
/// Strips accessor wording from every schema and parameter description in the finished document.
/// A document transformer rather than a schema one, because document transformers run last: by
/// then the XML comment descriptions are in place, including the ones ASP.NET Core puts next to a
/// property's <c>$ref</c> rather than on a schema.
/// </summary>
/// <param name="prefixes">The wording to remove, tried in order.</param>
internal sealed class DescriptionCleanupDocumentTransformer(IReadOnlyList<string> prefixes)
    : IOpenApiDocumentTransformer
{
    public Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        HashSet<IOpenApiSchema> visited = new(ReferenceEqualityComparer.Instance);
        foreach (IOpenApiSchema schema in document.Components?.Schemas?.Values ?? [])
        {
            Clean(schema, visited);
        }

        IEnumerable<OpenApiOperation> operations = (document.Paths?.Values ?? Enumerable.Empty<IOpenApiPathItem>())
            .SelectMany(path => path.Operations?.Values ?? Enumerable.Empty<OpenApiOperation>());
        foreach (OpenApiOperation operation in operations)
        {
            foreach (IOpenApiParameter parameter in operation.Parameters ?? [])
            {
                if (parameter is OpenApiParameter concrete)
                {
                    concrete.Description = Strip(concrete.Description);
                    Clean(concrete.Schema, visited);
                }
            }

            // Microsoft.OpenApi 2.x types the media types as OpenApiMediaType and 3.x as
            // IOpenApiMediaType; selecting the schema straight away compiles against both.
            IEnumerable<IOpenApiSchema?> bodies =
                (operation.RequestBody?.Content?.Values.Select(content => content.Schema) ?? [])
                .Concat((operation.Responses?.Values ?? Enumerable.Empty<IOpenApiResponse>())
                    .SelectMany(response =>
                        response.Content?.Values.Select(content => content.Schema) ?? []));
            foreach (IOpenApiSchema? body in bodies)
            {
                Clean(body, visited);
            }
        }

        return Task.CompletedTask;
    }

    private void Clean(IOpenApiSchema? schema, HashSet<IOpenApiSchema> visited)
    {
        switch (schema)
        {
            // A reference's own description (a property's summary next to its $ref) is cleaned
            // here; its target is a component, cleaned on its own.
            case OpenApiSchemaReference reference:
                reference.Reference.Description = Strip(reference.Reference.Description);
                return;
            case OpenApiSchema concrete when visited.Add(concrete):
                concrete.Description = Strip(concrete.Description);
                foreach (IOpenApiSchema property in concrete.Properties?.Values ?? [])
                {
                    Clean(property, visited);
                }

                IEnumerable<IOpenApiSchema> children = (concrete.AllOf ?? [])
                    .Concat(concrete.AnyOf ?? [])
                    .Concat(concrete.OneOf ?? []);
                foreach (IOpenApiSchema child in children)
                {
                    Clean(child, visited);
                }

                Clean(concrete.Items, visited);
                Clean(concrete.AdditionalProperties, visited);
                return;
            default:
                return;
        }
    }

    private string? Strip(string? description)
    {
        if (string.IsNullOrEmpty(description))
        {
            return description;
        }

        foreach (string prefix in prefixes)
        {
            if (description.Length > prefix.Length
                && description.StartsWith(prefix, StringComparison.Ordinal))
            {
                return char.ToUpperInvariant(description[prefix.Length])
                       + description[(prefix.Length + 1)..];
            }
        }

        return description;
    }
}
