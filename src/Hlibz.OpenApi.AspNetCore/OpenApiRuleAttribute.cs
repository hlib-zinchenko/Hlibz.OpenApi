namespace Hlibz.OpenApi;

/// <summary>
/// Adds a rule, in words, to the schema of the type, property or parameter it sits on. For rules
/// no attribute of their own can carry, such as a cross-field check in
/// <c>IValidatableObject.Validate</c>:
/// <c>[OpenApiRule("End must not be earlier than Start.")]</c> on the request type.
/// </summary>
/// <param name="text">The rule, as a sentence.</param>
[AttributeUsage(
    AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Property
    | AttributeTargets.Field | AttributeTargets.Parameter,
    AllowMultiple = true)]
public sealed class OpenApiRuleAttribute(string text) : Attribute, IOpenApiRule
{
    /// <summary>Gets the rule, as a sentence.</summary>
    public string Text { get; } = text;

    /// <inheritdoc />
    public void Describe(OpenApiSchema schema) => RuleText.Add(schema, Text);
}
