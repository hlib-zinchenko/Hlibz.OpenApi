using System.ComponentModel.DataAnnotations;

using Hlibz.OpenApi;

using Microsoft.OpenApi;

namespace Atlas.Api.Rules;

/// <summary>
/// Rejects an id's default value (the empty GUID, or <c>0</c>). Describes itself with JSON
/// Schema's <c>not</c> keyword, so a client generator can enforce it too, plus a sentence.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class NotDefaultAttribute : ValidationAttribute, IOpenApiRule
{
    public NotDefaultAttribute()
        : base("The {0} field must not be the empty id.")
    {
    }

    public override bool IsValid(object? value) =>
        value is null || !value.Equals(Activator.CreateInstance(value.GetType()));

    public void Describe(OpenApiSchema schema)
    {
        ArgumentNullException.ThrowIfNull(schema);

        schema.Not = new OpenApiSchema
        {
            Const = schema.Format == "uuid" ? Guid.Empty.ToString() : "0",
        };
        RuleText.Add(schema, "Must not be the empty id.");
    }
}
