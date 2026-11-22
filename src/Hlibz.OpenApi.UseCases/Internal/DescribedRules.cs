using System.Globalization;
using System.Text.Json.Nodes;

namespace Hlibz.OpenApi.UseCases.Internal;

/// <summary>
/// Reads the <see cref="IDescribedRule"/>s on types, members and parameters, as the
/// <see cref="IOpenApiRule"/>s Hlibz.OpenApi.AspNetCore writes: a rule source for
/// <see cref="OpenApiRuleOptions.Sources"/>, which keeps what it reads.
/// </summary>
internal static class DescribedRules
{
    /// <summary>
    /// Gets the rules on <paramref name="provider"/> (inherited ones included), each described once.
    /// </summary>
    /// <param name="provider">A type, member or parameter.</param>
    /// <returns>The rules; empty when there are none.</returns>
    public static IEnumerable<IOpenApiRule> For(ICustomAttributeProvider provider) =>
        provider.GetCustomAttributes(inherit: true)
            .OfType<IDescribedRule>()
            .Select(static rule =>
            {
                RuleDescription description = new();
                rule.Describe(description);
                return new DescribedRule(description);
            });
}

/// <summary>
/// One rule's <see cref="RuleDescription"/>, written into a schema: its keywords where JSON Schema
/// has them, and its sentences through <see cref="RuleText.Add"/> (<c>description</c> and
/// <c>x-rules</c>).
/// </summary>
/// <param name="Description">The rule's description.</param>
internal sealed record DescribedRule(RuleDescription Description) : IOpenApiRule
{
    /// <inheritdoc/>
    public void Describe(OpenApiSchema schema)
    {
        ArgumentNullException.ThrowIfNull(schema);

        RuleDescription rule = Description;
        schema.Pattern = rule.Pattern ?? schema.Pattern;
        schema.MinLength = rule.MinLength ?? schema.MinLength;
        schema.MaxLength = rule.MaxLength ?? schema.MaxLength;
        schema.MinItems = rule.MinItems ?? schema.MinItems;
        schema.MaxItems = rule.MaxItems ?? schema.MaxItems;
        schema.UniqueItems = rule.UniqueItems ?? schema.UniqueItems;
        schema.Minimum = Number(rule.Minimum) ?? schema.Minimum;
        schema.Maximum = Number(rule.Maximum) ?? schema.Maximum;
        schema.ExclusiveMinimum = Number(rule.ExclusiveMinimum) ?? schema.ExclusiveMinimum;
        schema.ExclusiveMaximum = Number(rule.ExclusiveMaximum) ?? schema.ExclusiveMaximum;
        if (rule.NotConst is { } value)
        {
            schema.Not = new OpenApiSchema { Const = Constant(value) };
        }

        foreach (string text in rule.Texts)
        {
            RuleText.Add(schema, text);
        }
    }

    private static string? Number(decimal? value) =>
        value?.ToString(CultureInfo.InvariantCulture);

    private static string Constant(JsonNode value) =>
        value is JsonValue scalar && scalar.TryGetValue(out string? text) ? text : value.ToJsonString();
}
