using Hlibz.OpenApi.Internal;

namespace Hlibz.OpenApi;

/// <summary>Registers <see cref="IOpenApiRule"/> support on an OpenAPI document.</summary>
public static class OpenApiRuleOptionsExtensions
{
    /// <summary>
    /// Lets every <see cref="IOpenApiRule"/> attribute (your own validation attributes, and
    /// <see cref="OpenApiRuleAttribute"/>) describe itself in the schema of the type, property or
    /// parameter it sits on. A parameter's rules are also added to the parameter's own
    /// description, which is what ReDoc and Swagger UI show for it.
    /// </summary>
    /// <param name="options">The document's options.</param>
    /// <returns>The same <paramref name="options"/>, for chaining.</returns>
    public static OpenApiOptions AddOpenApiRules(this OpenApiOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return options
            .AddSchemaTransformer(new OpenApiRuleSchemaTransformer())
            .AddOperationTransformer(new OpenApiRuleParameterOperationTransformer());
    }
}
