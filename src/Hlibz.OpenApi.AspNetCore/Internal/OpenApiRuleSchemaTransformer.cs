using System.Reflection;

using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace Hlibz.OpenApi.Internal;

/// <summary>
/// Lets every <see cref="IOpenApiRule"/> attribute describe the schema it applies to: one on a
/// property (body or <c>[AsParameters]</c>) or a parameter describes that property's or
/// parameter's schema, and one on a type describes the type's own schema.
/// </summary>
/// <remarks>
/// A property whose type is a component schema (an enum, a DTO) ends up as a <c>$ref</c>, and
/// anything written on its schema would land on the component every other use shares. For one,
/// a rule describes a scratch schema instead and only its sentences are kept, next to the
/// <c>$ref</c>, the way ASP.NET Core places the property's XML summary. The rules are also
/// parked under <see cref="PendingRulesKey"/>, for a later transformer that inlines the schema
/// after all (<see cref="StronglyTypedIdSchemaTransformer"/>) to apply in full.
/// </remarks>
internal sealed class OpenApiRuleSchemaTransformer : IOpenApiSchemaTransformer
{
    /// <summary>The metadata key the rules of a would-be <c>$ref</c> are parked under.</summary>
    internal const string PendingRulesKey = "x-hlibz-pending-rules";

    private const string SchemaIdKey = "x-schema-id";

    private const string RefDescriptionKey = "x-ref-description";

    public Task TransformAsync(
        OpenApiSchema schema,
        OpenApiSchemaTransformerContext context,
        CancellationToken cancellationToken)
    {
        IOpenApiRule[] rules = [.. FindRules(context)];
        if (rules.Length == 0)
        {
            return Task.CompletedTask;
        }

        if (context.JsonPropertyInfo is null || !IsReference(schema))
        {
            foreach (IOpenApiRule rule in rules)
            {
                rule.Describe(schema);
            }

            return Task.CompletedTask;
        }

        OpenApiSchema scratch = new();
        foreach (IOpenApiRule rule in rules)
        {
            rule.Describe(scratch);
        }

        IDictionary<string, object> metadata = schema.Metadata!;
        string? description = metadata.TryGetValue(RefDescriptionKey, out object? existing)
            ? existing as string
            : null;
        foreach (string text in RuleText.Read(scratch))
        {
            description = RuleText.Append(description, text);
        }

        if (description is not null)
        {
            metadata[RefDescriptionKey] = description;
        }

        metadata[PendingRulesKey] = rules;

        return Task.CompletedTask;
    }

    private static bool IsReference(OpenApiSchema schema) =>
        schema.Metadata?.TryGetValue(SchemaIdKey, out object? id) == true
        && !string.IsNullOrEmpty(id as string);

    private static IEnumerable<IOpenApiRule> FindRules(OpenApiSchemaTransformerContext context)
    {
        if (context.JsonPropertyInfo is { } property)
        {
            return property.AttributeProvider?.GetCustomAttributes(inherit: true)
                       .OfType<IOpenApiRule>()
                   ?? [];
        }

        IEnumerable<IOpenApiRule> typeRules = Nullable.GetUnderlyingType(context.JsonTypeInfo.Type)
            is { } underlying
            ? TypeRules(underlying)
            : TypeRules(context.JsonTypeInfo.Type);

        // Only the parameter's own schema: an array parameter's item schema comes with the same
        // ParameterDescription, and must not get the array's rules.
        if (ParameterSchemas.OwnType(context) is not null
            && context.ParameterDescription?.ParameterDescriptor
                is IParameterInfoParameterDescriptor { ParameterInfo: { } parameter })
        {
            return FindParameterRules(parameter).Concat(typeRules).Distinct();
        }

        return typeRules;
    }

    private static IEnumerable<IOpenApiRule> TypeRules(Type type) =>
        type.GetCustomAttributes(inherit: true).OfType<IOpenApiRule>();

    /// <summary>
    /// Reads an <c>[AsParameters]</c> property's attributes from the property itself: ASP.NET Core
    /// surfaces it as a <see cref="ParameterInfo"/> whose member is the property or, for a
    /// constructor-bound type, the type declaring a property of the same name.
    /// </summary>
    private static IEnumerable<IOpenApiRule> FindParameterRules(ParameterInfo parameter)
    {
        IEnumerable<object> attributes = parameter.GetCustomAttributes(inherit: true);
        if (parameter.Member is Type type
            && parameter.Name is { } name
            && type.GetProperty(name) is { } property)
        {
            attributes = attributes.Concat(property.GetCustomAttributes(inherit: true));
        }

        return attributes.OfType<IOpenApiRule>().Distinct();
    }
}
