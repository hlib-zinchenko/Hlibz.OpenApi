using Hlibz.OpenApi.UseCases.Internal;

namespace Hlibz.OpenApi;

/// <summary>Publishes Hlibz.UseCases in an OpenAPI document.</summary>
public static class UseCaseOpenApiOptionsExtensions
{
    /// <summary>
    /// Publishes what use cases say about themselves:
    /// <list type="bullet">
    /// <item>every <see cref="IDescribedRule"/> attribute (custom validation attributes,
    /// <see cref="RuleSummaryAttribute"/>) as keywords and text in the schema of the property,
    /// record parameter, parameter or type it sits on. It turns on
    /// <see cref="OpenApiRuleOptionsExtensions.AddOpenApiRules(OpenApiOptions)"/> with these rules
    /// as one more source, so they are written exactly as <see cref="IOpenApiRule"/>s are, and
    /// DataAnnotations on record constructor parameters are written too;</item>
    /// <item>a <c>400</c> on every operation whose handler takes a use case handler
    /// (<c>IUseCaseHandler&lt;TUseCase, ...&gt;</c> or its own interface) for a use case that can
    /// fail validation, unless the operation already documents one.</item>
    /// </list>
    /// </summary>
    /// <param name="options">The document's options.</param>
    /// <param name="configure">Configures the <c>400</c>.</param>
    /// <returns>The same <paramref name="options"/>, for chaining.</returns>
    /// <remarks>
    /// A use case's rules reach the document only on types that are part of the HTTP contract:
    /// bind the use case itself as the body, or give it a record that is also the body.
    /// </remarks>
    public static OpenApiOptions AddUseCases(
        this OpenApiOptions options,
        Action<UseCaseOpenApiOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(options);

        UseCaseOpenApiOptions settings = new();
        configure?.Invoke(settings);
        ArgumentNullException.ThrowIfNull(settings.ValidationResponseType);

        return options
            .AddOpenApiRules(static rules => rules.Sources.Add(DescribedRules.For))
            .AddOperationTransformer(new UseCaseValidationOperationTransformer(
                settings.ValidationResponseType,
                settings.ValidationContentType,
                settings.ValidationDescription));
    }
}
