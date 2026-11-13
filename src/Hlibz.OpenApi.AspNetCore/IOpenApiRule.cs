namespace Hlibz.OpenApi;

/// <summary>
/// A validation attribute that says what it checks in the OpenAPI schema of the property or
/// parameter it sits on. Registered with <see cref="OpenApiRuleOptionsExtensions.AddOpenApiRules"/>.
/// </summary>
/// <remarks>
/// DataAnnotations' own attributes (<c>[Required]</c>, <c>[Range]</c>, <c>[RegularExpression]</c>,
/// ...) already reach the schema; a custom <c>ValidationAttribute</c> doesn't. Implement this on
/// one and it writes its own keywords and text. Prefer a real JSON Schema keyword where one exists
/// (<c>pattern</c>, <c>not</c>, <c>uniqueItems</c>, <c>exclusiveMinimum</c>), so client generators
/// can enforce it, and add a sentence with <see cref="RuleText.Add"/> for people reading the docs.
/// Each attribute is read once and the same instance describes every schema it applies to, in
/// every document, so <see cref="Describe"/> writes only to the schema, never to the rule itself.
/// </remarks>
/// <example>
/// <code>
/// public sealed class StrongPasswordAttribute : ValidationAttribute, IOpenApiRule
/// {
///     public override bool IsValid(object? value) => ...;
///
///     public void Describe(OpenApiSchema schema)
///     {
///         schema.Pattern = "^(?=.*[a-z])(?=.*[A-Z])(?=.*\\d).*$";
///         RuleText.Add(schema, "Must contain a lowercase and an uppercase letter and a digit.");
///     }
/// }
/// </code>
/// </example>
public interface IOpenApiRule
{
    /// <summary>
    /// Writes this rule into <paramref name="schema"/>: the property's or parameter's own schema.
    /// </summary>
    /// <param name="schema">The schema to describe the rule in.</param>
    void Describe(OpenApiSchema schema);
}
