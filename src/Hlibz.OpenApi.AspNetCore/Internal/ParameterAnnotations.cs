using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Reflection;

namespace Hlibz.OpenApi.Internal;

/// <summary>
/// DataAnnotations' own attributes on a positional record's constructor parameter, as
/// <see cref="IOpenApiRule"/>s. Microsoft.AspNetCore.OpenApi reads them from properties only, so a
/// record written <c>record CreateCity([MaxLength(60)] string Name)</c> would publish none of the
/// rules its validation enforces. Each is written as Microsoft.AspNetCore.OpenApi writes the same
/// attribute on a property.
/// </summary>
internal static class ParameterAnnotations
{
    private static readonly ConcurrentDictionary<(MemberInfo Member, int Position), IOpenApiRule[]> Cache = new();

    /// <summary>Gets the rules for a record parameter's DataAnnotations.</summary>
    /// <param name="parameter">The constructor parameter behind a property.</param>
    /// <returns>The rules; empty when there are none.</returns>
    public static IOpenApiRule[] For(ParameterInfo parameter) =>
        Cache.GetOrAdd(
            (parameter.Member, parameter.Position),
            static (_, value) =>
            [
                .. value.GetCustomAttributes<ValidationAttribute>(inherit: true)
                    .Select(Rule)
                    .OfType<IOpenApiRule>(),
            ],
            parameter);

    private static AnnotationRule? Rule(ValidationAttribute attribute) => attribute switch
    {
        RangeAttribute range when IsNumber(range.OperandType) => new AnnotationRule(schema => SetRange(schema, range)),
        MinLengthAttribute min => new AnnotationRule(schema => SetLength(schema, min.Length, max: null)),
        MaxLengthAttribute max => new AnnotationRule(schema => SetLength(schema, min: null, max.Length)),
        LengthAttribute length => new AnnotationRule(schema => SetLength(schema, length.MinimumLength, length.MaximumLength)),
        StringLengthAttribute text => new AnnotationRule(schema =>
        {
            schema.MinLength = text.MinimumLength > 0 ? text.MinimumLength : schema.MinLength;
            schema.MaxLength = text.MaximumLength;
        }),
        RegularExpressionAttribute pattern => new AnnotationRule(schema => schema.Pattern = pattern.Pattern),
        UrlAttribute => new AnnotationRule(schema => schema.Format = "uri"),
        Base64StringAttribute => new AnnotationRule(schema => schema.Format = "byte"),
        _ => null,
    };

    private static void SetRange(OpenApiSchema schema, RangeAttribute range)
    {
        string minimum = Invariant(range.Minimum);
        string maximum = Invariant(range.Maximum);
        if (range.MinimumIsExclusive)
        {
            schema.ExclusiveMinimum = minimum;
        }
        else
        {
            schema.Minimum = minimum;
        }

        if (range.MaximumIsExclusive)
        {
            schema.ExclusiveMaximum = maximum;
        }
        else
        {
            schema.Maximum = maximum;
        }
    }

    private static void SetLength(OpenApiSchema schema, int? min, int? max)
    {
        if (schema.Type is { } type && type.HasFlag(JsonSchemaType.Array))
        {
            schema.MinItems = min ?? schema.MinItems;
            schema.MaxItems = max ?? schema.MaxItems;
        }
        else
        {
            schema.MinLength = min ?? schema.MinLength;
            schema.MaxLength = max ?? schema.MaxLength;
        }
    }

    private static bool IsNumber(Type type) =>
        type == typeof(int) || type == typeof(long) || type == typeof(double) || type == typeof(decimal)
        || type == typeof(float) || type == typeof(short) || type == typeof(byte);

    private static string Invariant(object value) =>
        Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;

    /// <summary>A DataAnnotations attribute, written as a schema keyword.</summary>
    private sealed class AnnotationRule(Action<OpenApiSchema> describe) : IOpenApiRule
    {
        public void Describe(OpenApiSchema schema) => describe(schema);
    }
}
