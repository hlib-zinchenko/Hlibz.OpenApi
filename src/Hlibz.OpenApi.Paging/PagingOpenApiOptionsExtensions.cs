using Hlibz.OpenApi.Paging.Internal;

namespace Hlibz.OpenApi;

/// <summary>Publishes Hlibz.Paging in an OpenAPI document.</summary>
public static class PagingOpenApiOptionsExtensions
{
    /// <summary>
    /// Describes every sort string marked <see cref="SortableByAttribute"/>, on the property,
    /// record parameter or parameter it sits on: a sentence naming the DTO's sortable properties,
    /// the format and the default order (in <c>description</c> and <c>x-rules</c>, as
    /// <see cref="IOpenApiRule"/>s are), and the default order as the schema's <c>default</c>.
    /// Properties are named as the attribute's errors name them
    /// (<see cref="SortableByAttribute.NamingPolicy"/>). It turns on
    /// <see cref="OpenApiRuleOptionsExtensions.AddOpenApiRules(OpenApiOptions)"/> with these rules
    /// as one more source.
    /// </summary>
    /// <param name="options">The document's options.</param>
    /// <returns>The same <paramref name="options"/>, for chaining.</returns>
    public static OpenApiOptions AddPaging(this OpenApiOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return options.AddOpenApiRules(static rules => rules.Sources.Add(SortableByRules.For));
    }
}
