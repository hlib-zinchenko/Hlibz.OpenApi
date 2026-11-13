using System.Text.Json.Nodes;

namespace Hlibz.OpenApi;

/// <summary>
/// Writes a rule's sentence into a schema, for <see cref="IOpenApiRule"/> implementations.
/// </summary>
public static class RuleText
{
    /// <summary>The extension every rule's sentence is listed in, for tools to read.</summary>
    public const string ExtensionName = "x-rules";

    private const string ParagraphBreak = "\n\n";

    /// <summary>
    /// Appends <paramref name="text"/> to the schema's <c>description</c> (what ReDoc and Swagger
    /// UI show) as its own paragraph, and to its <see cref="ExtensionName"/> list. Adding the same
    /// sentence twice is a no-op.
    /// </summary>
    /// <param name="schema">The schema to add the rule to.</param>
    /// <param name="text">The rule, as a sentence.</param>
    public static void Add(OpenApiSchema schema, string text)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        schema.Extensions ??= new Dictionary<string, IOpenApiExtension>(StringComparer.Ordinal);
        JsonArray rules = schema.Extensions.TryGetValue(ExtensionName, out IOpenApiExtension? existing)
                          && existing is JsonNodeExtension { Node: JsonArray array }
            ? array
            : [];
        if (rules.Any(rule => rule?.GetValue<string>() == text))
        {
            return;
        }

        // As a JsonNode: the generic Add<T> isn't trimming-safe.
        rules.Add((JsonNode?)JsonValue.Create(text));
        schema.Extensions[ExtensionName] = new JsonNodeExtension(rules);
        schema.Description = Append(schema.Description, text);
    }

    /// <summary>Gets the rule sentences listed in a schema's <see cref="ExtensionName"/>.</summary>
    /// <param name="schema">The schema to read.</param>
    /// <returns>The sentences, in the order they were added.</returns>
    internal static IReadOnlyList<string> Read(OpenApiSchema schema) =>
        schema.Extensions?.TryGetValue(ExtensionName, out IOpenApiExtension? existing) == true
        && existing is JsonNodeExtension { Node: JsonArray array }
            ? [.. array.Select(rule => rule?.GetValue<string>()).OfType<string>()]
            : [];

    /// <summary>Appends a sentence to a description as its own paragraph, once.</summary>
    /// <param name="description">The description, if any.</param>
    /// <param name="text">The sentence.</param>
    /// <returns>The new description.</returns>
    internal static string Append(string? description, string text)
    {
        if (string.IsNullOrEmpty(description))
        {
            return text;
        }

        return Paragraphs(description).Contains(text, StringComparer.Ordinal)
            ? description
            : $"{description}{ParagraphBreak}{text}";
    }

    /// <summary>Removes a sentence's paragraph from a description.</summary>
    /// <param name="description">The description, if any.</param>
    /// <param name="text">The sentence.</param>
    /// <returns>The new description, or <see langword="null"/> once nothing is left.</returns>
    internal static string? Remove(string? description, string text)
    {
        if (string.IsNullOrEmpty(description))
        {
            return description;
        }

        string remaining = string.Join(
            ParagraphBreak,
            Paragraphs(description).Where(paragraph => paragraph != text));
        return remaining.Length == 0 ? null : remaining;
    }

    private static string[] Paragraphs(string description) =>
        description.Split(ParagraphBreak);
}
