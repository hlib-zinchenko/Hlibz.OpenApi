using System.Collections;
using System.ComponentModel.DataAnnotations;

using Hlibz.OpenApi;

using Microsoft.OpenApi;

namespace Atlas.Api.Rules;

/// <summary>
/// Rejects a list with a repeated item. JSON Schema has the exact keyword for it,
/// <c>uniqueItems</c>.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class DistinctItemsAttribute : ValidationAttribute, IOpenApiRule
{
    public DistinctItemsAttribute()
        : base("The {0} field must not list an item twice.")
    {
    }

    public override bool IsValid(object? value)
    {
        if (value is not IEnumerable items)
        {
            return true;
        }

        List<object?> list = [.. items.Cast<object?>()];
        return list.Distinct().Count() == list.Count;
    }

    public void Describe(OpenApiSchema schema)
    {
        ArgumentNullException.ThrowIfNull(schema);

        schema.UniqueItems = true;
        RuleText.Add(schema, "No item may appear twice.");
    }
}
