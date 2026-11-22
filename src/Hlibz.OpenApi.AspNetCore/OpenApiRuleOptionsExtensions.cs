using System.Runtime.CompilerServices;

using Hlibz.OpenApi.Internal;

namespace Hlibz.OpenApi;

/// <summary>Registers <see cref="IOpenApiRule"/> support on an OpenAPI document.</summary>
public static class OpenApiRuleOptionsExtensions
{
    /// <summary>The rule options of each document that has called <c>AddOpenApiRules</c>.</summary>
    private static readonly ConditionalWeakTable<OpenApiOptions, OpenApiRuleOptions> Registered = [];

    /// <summary>
    /// Lets every <see cref="IOpenApiRule"/> attribute (your own validation attributes, and
    /// <see cref="OpenApiRuleAttribute"/>) describe itself in the schema of the type, property or
    /// parameter it sits on. A parameter's rules are also added to the parameter's own
    /// description, which is what ReDoc and Swagger UI show for it. Also writes the
    /// DataAnnotations attributes on a positional record's constructor parameters
    /// (<c>record CreateCity([MaxLength(60)] string Name)</c>), which Microsoft.AspNetCore.OpenApi
    /// reads from properties only.
    /// </summary>
    /// <param name="options">The document's options.</param>
    /// <returns>The same <paramref name="options"/>, for chaining.</returns>
    public static OpenApiOptions AddOpenApiRules(this OpenApiOptions options) =>
        options.AddOpenApiRules(static _ => { });

    /// <summary>
    /// Lets every <see cref="IOpenApiRule"/> attribute describe itself, as
    /// <see cref="AddOpenApiRules(OpenApiOptions)"/> does, plus the rules of
    /// <see cref="OpenApiRuleOptions.Sources"/>. Calling it again on the same document only
    /// runs <paramref name="configure"/>: the rules are written once.
    /// </summary>
    /// <param name="options">The document's options.</param>
    /// <param name="configure">Adds rule sources.</param>
    /// <returns>The same <paramref name="options"/>, for chaining.</returns>
    public static OpenApiOptions AddOpenApiRules(
        this OpenApiOptions options,
        Action<OpenApiRuleOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(configure);

        if (!Registered.TryGetValue(options, out OpenApiRuleOptions? rules))
        {
            rules = new OpenApiRuleOptions();
            Registered.Add(options, rules);
            RuleReader reader = new(rules);
            options
                .AddSchemaTransformer(new OpenApiRuleSchemaTransformer(reader))
                .AddOperationTransformer(new OpenApiRuleParameterOperationTransformer(reader));
        }

        configure(rules);
        return options;
    }
}
