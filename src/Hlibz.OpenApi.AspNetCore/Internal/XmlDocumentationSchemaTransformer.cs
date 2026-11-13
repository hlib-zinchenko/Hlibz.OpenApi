using System.Reflection;

using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Hlibz.OpenApi.Internal;

/// <summary>
/// Publishes XML <c>&lt;summary&gt;</c>s as schema descriptions: a type's on its own schema, a
/// property's on the property, or next to the property's <c>$ref</c> when its schema becomes a
/// component, so one property's summary never lands on the component every other use shares.
/// </summary>
/// <remarks>
/// It replaces whatever description is there (Microsoft.AspNetCore.OpenApi's generator writes one
/// for public types, with line breaks and crefs as types), keeping the rule text
/// <see cref="OpenApiRuleSchemaTransformer"/> appended, so it may be registered before or after it.
/// </remarks>
internal sealed class XmlDocumentationSchemaTransformer : IOpenApiSchemaTransformer
{
    private const string SchemaIdKey = "x-schema-id";

    private const string RefDescriptionKey = "x-ref-description";

    public Task TransformAsync(
        OpenApiSchema schema,
        OpenApiSchemaTransformerContext context,
        CancellationToken cancellationToken)
    {
        Type type = Nullable.GetUnderlyingType(context.JsonTypeInfo.Type) ?? context.JsonTypeInfo.Type;
        bool reference = IsReference(schema);

        // A type's summary describes the type, so it goes on the type's own schema: not on a
        // property that stays inline, nor on a route, query or header parameter, which the
        // property's or parameter's own description is for.
        if ((context.JsonPropertyInfo is null || reference)
            && !IsRequestParameter(context)
            && XmlDocumentation.TypeSummary(type) is { } typeSummary)
        {
            schema.Description = WithRules(typeSummary, RuleText.Read(schema));
        }

        if (context.JsonPropertyInfo?.AttributeProvider is not PropertyInfo property
            || XmlDocumentation.PropertySummary(property) is not { } summary)
        {
            return Task.CompletedTask;
        }

        if (reference)
        {
            schema.Metadata![RefDescriptionKey] = WithRules(summary, PendingRuleText(schema.Metadata));
        }
        else
        {
            schema.Description = WithRules(summary, RuleText.Read(schema));
        }

        return Task.CompletedTask;
    }

    /// <summary>A summary followed by the rule text already written next to it.</summary>
    /// <param name="summary">The XML summary.</param>
    /// <param name="rules">The rule text, in order.</param>
    /// <returns>The description.</returns>
    internal static string WithRules(string summary, IEnumerable<string> rules) =>
        rules.Aggregate(summary, RuleText.Append);

    private static bool IsRequestParameter(OpenApiSchemaTransformerContext context) =>
        ParameterSchemas.BoundType(context) is not null
        && context.ParameterDescription?.Source is { } source
        && source != BindingSource.Body
        && source != BindingSource.Form
        && source != BindingSource.FormFile;

    private static bool IsReference(OpenApiSchema schema) =>
        schema.Metadata?.TryGetValue(SchemaIdKey, out object? id) == true
        && !string.IsNullOrEmpty(id as string);

    /// <summary>The text of the rules <see cref="OpenApiRuleSchemaTransformer"/> parked on a would-be <c>$ref</c>.</summary>
    private static IReadOnlyList<string> PendingRuleText(IDictionary<string, object> metadata)
    {
        if (!metadata.TryGetValue(OpenApiRuleSchemaTransformer.PendingRulesKey, out object? pending)
            || pending is not IOpenApiRule[] rules)
        {
            return [];
        }

        OpenApiSchema scratch = new();
        foreach (IOpenApiRule rule in rules)
        {
            rule.Describe(scratch);
        }

        return RuleText.Read(scratch);
    }
}
