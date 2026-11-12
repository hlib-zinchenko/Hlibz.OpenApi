using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Hlibz.OpenApi.Internal;

/// <summary>
/// Publishes each strongly-typed ID as the schema of the value it wraps (a <c>CountryId</c> over a
/// <see cref="Guid"/> becomes <c>{ "type": "string", "format": "uuid" }</c>), inline rather than
/// as a component of its own.
/// </summary>
/// <param name="valueTypeOf">
/// Gets the wrapped type of an ID type, or <see langword="null"/> for any other type;
/// <see langword="null"/> for the default rule, <see cref="SingleValueStruct"/>.
/// </param>
internal sealed class StronglyTypedIdSchemaTransformer(Func<Type, Type?>? valueTypeOf)
    : IOpenApiSchemaTransformer
{
    /// <summary>The metadata key ASP.NET Core turns into a component schema and a <c>$ref</c>.</summary>
    private const string SchemaIdKey = "x-schema-id";

    /// <summary>The metadata key ASP.NET Core marks a nullable property's schema with.</summary>
    private const string NullablePropertyKey = "x-is-nullable-property";

    /// <summary>The metadata key ASP.NET Core puts a <c>$ref</c>'d property's summary under.</summary>
    private const string RefDescriptionKey = "x-ref-description";

    public async Task TransformAsync(
        OpenApiSchema schema,
        OpenApiSchemaTransformerContext context,
        CancellationToken cancellationToken)
    {
        Type? nullableOf = Nullable.GetUnderlyingType(context.JsonTypeInfo.Type);
        Type type = nullableOf ?? context.JsonTypeInfo.Type;
        Type? valueType = valueTypeOf is null
            ? DefaultValueTypeOf(type, isParameter: false, context.JsonTypeInfo.Options)
            : valueTypeOf(type);

        // A route or query parameter bound through TryParse (or an item of an array of them) is
        // described as a string; only its ParameterDescription still knows the ID type.
        if (valueType is null && ParameterSchemas.BoundType(context) is { } bound && bound != type)
        {
            nullableOf = Nullable.GetUnderlyingType(context.ParameterDescription!.Type);
            type = bound;
            valueType = valueTypeOf is null
                ? DefaultValueTypeOf(type, isParameter: true, context.JsonTypeInfo.Options)
                : valueTypeOf(type);
        }

        if (valueType is null)
        {
            await DescribeItemsAsync(schema, context, cancellationToken).ConfigureAwait(false);
            return;
        }

        OpenApiSchema value = await context
            .GetOrCreateSchemaAsync(valueType, parameterDescription: null, cancellationToken)
            .ConfigureAwait(false);

        bool nullable = nullableOf is not null
                        || (schema.Type is { } current && current.HasFlag(JsonSchemaType.Null));
        schema.Type = value.Type is { } valueSchemaType && nullable
            ? valueSchemaType | JsonSchemaType.Null
            : value.Type;
        schema.Format = value.Format;
        schema.Pattern = value.Pattern;
        schema.Properties?.Clear();
        schema.Required?.Clear();
        schema.AdditionalProperties = null;
        schema.Items = null;
        schema.AnyOf?.Clear();
        schema.OneOf?.Clear();
        schema.AllOf?.Clear();
        if (schema.Metadata is { } metadata)
        {
            Inline(schema, metadata);
        }
    }

    /// <summary>
    /// The shape of the default rule: a struct (not a primitive or an enum) whose only public
    /// instance property is <c>Value</c>. <see cref="DefaultValueTypeOf"/> adds the JSON check.
    /// </summary>
    /// <param name="type">The type to check.</param>
    /// <returns>The type of <c>Value</c>, or <see langword="null"/>.</returns>
    internal static Type? SingleValueStruct(Type type)
    {
        if (!type.IsValueType || type.IsPrimitive || type.IsEnum)
        {
            return null;
        }

        PropertyInfo[] properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        return properties is [{ Name: "Value" } property] ? property.PropertyType : null;
    }

    /// <summary>
    /// Makes the schema an inline one rather than a component, keeping what earlier transformers
    /// left for the <c>$ref</c> it would have been: the property's XML summary, and its rules.
    /// </summary>
    private static void Inline(OpenApiSchema schema, IDictionary<string, object> metadata)
    {
        metadata.Remove(SchemaIdKey);

        // Inlined, the schema's own "null" type says it's nullable; left in, this marker would
        // make ASP.NET Core wrap it in a redundant oneOf: [{ "type": "null" }, ...].
        metadata.Remove(NullablePropertyKey);

        if (metadata.Remove(RefDescriptionKey, out object? description))
        {
            schema.Description = description as string;
        }

        if (metadata.Remove(OpenApiRuleSchemaTransformer.PendingRulesKey, out object? pending)
            && pending is IOpenApiRule[] rules)
        {
            foreach (IOpenApiRule rule in rules)
            {
                rule.Describe(schema);
            }
        }
    }

    /// <summary>
    /// The default rule. In JSON it matches only types System.Text.Json writes as a single value:
    /// one without a converter is written as <c>{ "value": ... }</c>, which its schema already
    /// says. A route or query parameter is parsed from its string form, so it always matches.
    /// </summary>
    private static Type? DefaultValueTypeOf(
        Type type,
        bool isParameter,
        JsonSerializerOptions options) =>
        SingleValueStruct(type) is { } valueType
        && (isParameter || options.GetTypeInfo(type).Kind != JsonTypeInfoKind.Object)
            ? valueType
            : null;

    /// <summary>
    /// Gives an array of IDs bound from the query string its item schema: ASP.NET Core describes
    /// a <c>CountryId[]</c> parameter as <c>{ "type": "array" }</c> with no items at all, so there
    /// is no item schema to transform.
    /// </summary>
    private async Task DescribeItemsAsync(
        OpenApiSchema schema,
        OpenApiSchemaTransformerContext context,
        CancellationToken cancellationToken)
    {
        if (schema.Items is not null
            || ParameterSchemas.OwnType(context) is not { } parameterType
            || ParameterSchemas.ElementType(parameterType) is not { } element)
        {
            return;
        }

        Type item = Nullable.GetUnderlyingType(element) ?? element;
        Type? valueType = valueTypeOf is null
            ? DefaultValueTypeOf(item, isParameter: true, context.JsonTypeInfo.Options)
            : valueTypeOf(item);
        if (valueType is null)
        {
            return;
        }

        OpenApiSchema value = await context
            .GetOrCreateSchemaAsync(valueType, parameterDescription: null, cancellationToken)
            .ConfigureAwait(false);
        schema.Items = new OpenApiSchema
        {
            Type = value.Type,
            Format = value.Format,
            Pattern = value.Pattern,
        };
    }
}
