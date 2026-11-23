using System.ComponentModel;
using System.Text.Json.Nodes;

namespace Hlibz.OpenApi.Paging.Internal;

/// <summary>
/// Reads the <see cref="SortableByAttribute"/>s on types, members and parameters, as the
/// <see cref="IOpenApiRule"/>s Hlibz.OpenApi.AspNetCore writes: a rule source for
/// <see cref="OpenApiRuleOptions.Sources"/>.
/// </summary>
internal static class SortableByRules
{
    /// <summary>Gets the sort rules on <paramref name="provider"/>.</summary>
    /// <param name="provider">A type, member or parameter.</param>
    /// <returns>The rules; empty when there are none.</returns>
    public static IEnumerable<IOpenApiRule> For(ICustomAttributeProvider provider) =>
        provider.GetCustomAttributes(inherit: true)
            .OfType<SortableByAttribute>()
            .Select(static attribute => new SortableByRule(attribute));
}

/// <summary>
/// One <see cref="SortableByAttribute"/>, written into a schema: what can be sorted by, the format
/// and the default order as a sentence, and the default order as <c>default</c>.
/// </summary>
/// <param name="Attribute">The attribute.</param>
internal sealed record SortableByRule(SortableByAttribute Attribute) : IOpenApiRule
{
    /// <inheritdoc/>
    public void Describe(OpenApiSchema schema)
    {
        ArgumentNullException.ThrowIfNull(schema);

        SortableDto dto = SortableDto.For(Attribute.DtoType);
        string direction = dto.DefaultSorting.SortDirection == ListSortDirection.Descending
            ? "desc"
            : "asc";
        string defaultOrder = $"{Attribute.ConvertName(dto.DefaultSorting.SortBy)}:{direction}";
        string text =
            $"Sort by: {string.Join(", ", dto.Properties.Select(Attribute.ConvertName))}. "
            + "Comma-separated, each optionally followed by :asc (the default) or :desc. "
            + $"Default: {defaultOrder}.";
        schema.Default = JsonValue.Create(defaultOrder);
        RuleText.Add(schema, text);
    }
}
