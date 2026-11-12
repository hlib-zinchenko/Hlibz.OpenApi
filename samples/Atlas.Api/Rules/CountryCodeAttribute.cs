using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

using Hlibz.OpenApi;

using Microsoft.OpenApi;

namespace Atlas.Api.Rules;

/// <summary>
/// An ISO 3166-1 alpha-2 country code: two capital letters. A <c>pattern</c> any client can
/// check, plus the sentence people read.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed partial class CountryCodeAttribute : ValidationAttribute, IOpenApiRule
{
    private const string Pattern = "^[A-Z]{2}$";

    public CountryCodeAttribute()
        : base("The {0} field must be an ISO 3166-1 alpha-2 code, such as UA.")
    {
    }

    public override bool IsValid(object? value) =>
        value is null || (value is string code && CodeRegex().IsMatch(code));

    public void Describe(OpenApiSchema schema)
    {
        ArgumentNullException.ThrowIfNull(schema);

        schema.Pattern = Pattern;
        RuleText.Add(schema, "An ISO 3166-1 alpha-2 code: two capital letters, such as `UA`.");
    }

    [GeneratedRegex(Pattern)]
    private static partial Regex CodeRegex();
}
