using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Hlibz.OpenApi.Internal;

/// <summary>
/// Publishes each strongly-typed ID as the schema of the value it wraps (a <c>CountryId</c> over a
/// <see cref="Guid"/> becomes <c>{ "type": "string", "format": "uuid" }</c>), inline rather than
/// as a component of its own unless <paramref name="asComponents"/>.
/// </summary>
/// <param name="valueTypeOf">
/// Gets the wrapped type of an ID type, or <see langword="null"/> for any other type;
/// <see langword="null"/> for the default rule, <see cref="SingleValueStruct"/>.
/// </param>
/// <param name="asComponents">
/// Whether an ID that would be a component stays one, so properties keep their <c>$ref</c> and
/// ASP.NET Core expresses a nullable one as <c>oneOf</c> with <c>null</c>.
/// </param>
internal sealed class StronglyTypedIdSchemaTransformer(Func<Type, Type?>? valueTypeOf, bool asComponents)
    : IOpenApiSchemaTransformer
{
    /// <summary>The metadata key ASP.NET Core turns into a component schema and a <c>$ref</c>.</summary>
    private const string SchemaIdKey = "x-schema-id";

    /// <summary>The metadata key ASP.NET Core marks a nullable property's schema with.</summary>
    private const string NullablePropertyKey = "x-is-nullable-property";

    /// <summary>The metadata key ASP.NET Core puts a <c>$ref</c>'d property's summary under.</summary>
    private const string RefDescriptionKey = "x-ref-description";

    /// <summary>The <see cref="SingleValueStruct"/> answer for each struct, looked up once.</summary>
    private static readonly ConcurrentDictionary<Type, Type?> SingleValueStructs = new();

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

        // A component is shared by nullable and non-nullable uses alike; ASP.NET Core wraps the
        // nullable ones' $ref in a oneOf with null.
        bool component = asComponents && IsReference(schema);
        bool nullable = !component
                        && (nullableOf is not null
                            || (schema.Type is { } current && current.HasFlag(JsonSchemaType.Null)));
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
        if (!component && schema.Metadata is { } metadata)
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
    internal static Type? SingleValueStruct(Type type) =>
        type.IsValueType && !type.IsPrimitive && !type.IsEnum
            ? SingleValueStructs.GetOrAdd(type, static key => ReadSingleValue(key))
            : null;

    // An ID's Value is read by its JSON converter or TryParse, so trimming keeps it; one that
    // was trimmed would only leave the ID's schema as ASP.NET Core wrote it. A custom
    // StronglyTypedIdOptions.ValueTypeOf doesn't reflect at all.
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2070:'this' argument does not satisfy 'DynamicallyAccessedMembersAttribute' in call to target method",
        Justification = "A trimmed Value property only means the schema isn't rewritten.")]
    private static Type? ReadSingleValue(Type type)
    {
        PropertyInfo[] properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        return properties is [{ Name: "Value" } property] ? property.PropertyType : null;
    }

    private static bool IsReference(OpenApiSchema schema) =>
        schema.Metadata?.TryGetValue(SchemaIdKey, out object? id) == true
        && !string.IsNullOrEmpty(id as string);

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

        metadata.Remove(OpenApiRuleSchemaTransformer.PendingRuleTextKey);
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
