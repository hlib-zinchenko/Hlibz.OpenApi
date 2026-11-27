namespace Hlibz.OpenApi.Results.Internal;

/// <summary>
/// Turns the schemas inside a schema from
/// <see cref="OpenApiOperationTransformerContext.GetOrCreateSchemaAsync"/> into <c>$ref</c>s to
/// their components, as ASP.NET Core does for the schemas it generates itself: the transformer
/// context returns them unresolved, every nested type inline, so an error's details would write
/// an enum or a strongly-typed ID inline where the rest of the document refers to its component.
/// </summary>
internal static class SchemaReferences
{
    /// <summary>The metadata key ASP.NET Core names a schema's component with.</summary>
    internal const string SchemaIdKey = "x-schema-id";

    /// <summary>The metadata key ASP.NET Core marks a nullable property's schema with.</summary>
    private const string NullablePropertyKey = "x-is-nullable-property";

    /// <summary>The metadata key ASP.NET Core puts a <c>$ref</c>'d property's description under.</summary>
    private const string RefDescriptionKey = "x-ref-description";

    /// <summary>
    /// Replaces, in place, each schema inside <paramref name="schema"/> that has a component name
    /// with a <c>$ref</c> to that component, adding the component when the document lacks it.
    /// </summary>
    /// <param name="schema">The schema whose subschemas to resolve; itself left as it is.</param>
    /// <param name="document">The document the components are in.</param>
    public static void Resolve(OpenApiSchema schema, OpenApiDocument document)
    {
        if (schema.Properties is { } properties)
        {
            foreach (string name in properties.Keys.ToArray())
            {
                properties[name] = Reference(properties[name], document);
            }
        }

        if (schema.Items is { } items)
        {
            schema.Items = Reference(items, document);
        }

        if (schema.AdditionalProperties is { } additional)
        {
            schema.AdditionalProperties = Reference(additional, document);
        }

        Resolve(schema.AllOf, document);
        Resolve(schema.AnyOf, document);
        Resolve(schema.OneOf, document);
    }

    private static void Resolve(IList<IOpenApiSchema>? schemas, OpenApiDocument document)
    {
        for (int index = 0; schemas is not null && index < schemas.Count; index++)
        {
            schemas[index] = Reference(schemas[index], document);
        }
    }

    private static IOpenApiSchema Reference(IOpenApiSchema input, OpenApiDocument document)
    {
        if (input is not OpenApiSchema schema)
        {
            return input;
        }

        if (schema.Metadata is not { } metadata
            || !metadata.TryGetValue(SchemaIdKey, out object? value)
            || value is not string { Length: > 0 } id)
        {
            Resolve(schema, document);
            return schema;
        }

        // Added before its own subschemas are resolved, so a type that refers to itself stops here.
        if (document.Components?.Schemas?.ContainsKey(id) != true)
        {
            document.AddComponent(id, schema);
            Resolve(schema, document);
        }

        OpenApiSchemaReference reference = new(id, document);
        if (metadata.TryGetValue(RefDescriptionKey, out object? description) && description is string text)
        {
            reference.Description = text;
        }

        return metadata.TryGetValue(NullablePropertyKey, out object? nullable) && nullable is true
            ? new OpenApiSchema { OneOf = [new OpenApiSchema { Type = JsonSchemaType.Null }, reference] }
            : reference;
    }
}
