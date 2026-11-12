using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hlibz.OpenApi.AspNetCore.Tests;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class StrongPasswordAttribute : ValidationAttribute, IOpenApiRule
{
    public const string Text = "Must contain a lowercase and an uppercase letter and a digit.";

    public override bool IsValid(object? value) =>
        value is string password && password.Any(char.IsUpper) && password.Any(char.IsLower)
        && password.Any(char.IsDigit);

    public void Describe(Microsoft.OpenApi.OpenApiSchema schema)
    {
        schema.Pattern = "^(?=.*[a-z])(?=.*[A-Z])(?=.*\\d).*$";
        RuleText.Add(schema, Text);
    }
}

/// <summary>Changes a user's password.</summary>
public sealed class ChangePassword
{
    /// <summary>Gets or sets the user's new password.</summary>
    [StrongPassword]
    [MinLength(8)]
    public string Password { get; set; } = string.Empty;

    /// <summary>Gets or sets a value indicating whether every other session is signed out.</summary>
    public bool SignOutEverywhere { get; set; }
}

/// <summary>A date range.</summary>
[OpenApiRule("End must not be earlier than Start.")]
public sealed record DateRange(DateOnly Start, DateOnly End);

[OpenApiRule("Give at most one of Tags and Countries.")]
public sealed class SearchQuery
{
    [OpenApiRule("No tag twice.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1819", Justification = "Query arrays bind to arrays.")]
    public string[]? Tags { get; set; }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1819", Justification = "Query arrays bind to arrays.")]
    public CountryId[]? Countries { get; set; }

    /// <summary>Gets or sets the most rows to return.</summary>
    [OpenApiRule("Must be a multiple of 10.")]
    public int? Limit { get; set; }
}

[JsonConverter(typeof(CountryIdConverter))]
public readonly record struct CountryId(Guid Value) : IParsable<CountryId>
{
    public static CountryId Parse(string s, IFormatProvider? provider) => new(Guid.Parse(s, provider));

    public static bool TryParse(
        [NotNullWhen(true)] string? s,
        IFormatProvider? provider,
        out CountryId result)
    {
        bool parsed = Guid.TryParse(s, provider, out Guid value);
        result = new CountryId(value);
        return parsed;
    }
}

public sealed class CountryIdConverter : JsonConverter<CountryId>
{
    public override CountryId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        new(reader.GetGuid());

    public override void Write(Utf8JsonWriter writer, CountryId value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.Value);
}

[JsonConverter(typeof(OrderNumberConverter))]
public readonly record struct OrderNumber(int Value);

public sealed class OrderNumberConverter : JsonConverter<OrderNumber>
{
    public override OrderNumber Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        new(reader.GetInt32());

    public override void Write(Utf8JsonWriter writer, OrderNumber value, JsonSerializerOptions options) =>
        writer.WriteNumberValue(value.Value);
}

/// <summary>Has no converter, so it is written as <c>{ "value": ... }</c>.</summary>
public readonly record struct PlainWrapper(Guid Value);

public sealed record City(
    CountryId CountryId,
    CountryId? PreviousCountryId,
    OrderNumber Number,
    PlainWrapper Plain,
    string Name);

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class NotDefaultAttribute : ValidationAttribute, IOpenApiRule
{
    public const string Text = "Must not be the empty id.";

    public override bool IsValid(object? value) =>
        value is null || !value.Equals(Activator.CreateInstance(value.GetType()));

    public void Describe(Microsoft.OpenApi.OpenApiSchema schema)
    {
        schema.Not = new Microsoft.OpenApi.OpenApiSchema
        {
            Const = "00000000-0000-0000-0000-000000000000",
        };
        RuleText.Add(schema, Text);
    }
}

public enum Colour
{
    Red,
    Green,
}

public sealed class Paint
{
    /// <summary>Gets or sets the paint's colour.</summary>
    [OpenApiRule("Green is out of stock.")]
    public Colour Colour { get; set; }

    public Colour Trim { get; set; }

    /// <summary>Gets or sets the country the paint is made in.</summary>
    [NotDefault]
    public CountryId MadeIn { get; set; }
}

[Microsoft.Extensions.Validation.ValidatableType]
public sealed class Signup
{
    [Required]
    [MaxLength(20)]
    public string Name { get; set; } = string.Empty;
}

[Microsoft.Extensions.Validation.ValidatableType]
public sealed class Paging
{
    [Range(1, 100)]
    public int Limit { get; set; } = 10;
}

/// <summary>The body a custom <c>IProblemDetailsWriter</c> writes instead of <c>HttpValidationProblemDetails</c>.</summary>
public sealed record ErrorResponse(string Code, IReadOnlyList<string> Messages);

/// <summary>Validatable, but registered as a service, so validation skips it as a handler parameter.</summary>
[Microsoft.Extensions.Validation.ValidatableType]
public sealed class Settings
{
    [Range(1, 100)]
    public int PageSize { get; set; } = 10;
}
