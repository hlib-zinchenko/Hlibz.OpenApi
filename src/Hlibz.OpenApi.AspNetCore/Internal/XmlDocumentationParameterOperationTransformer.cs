using System.Reflection;

using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace Hlibz.OpenApi.Internal;

/// <summary>
/// Publishes the <c>&lt;summary&gt;</c> of each <c>[AsParameters]</c> property as its parameter's
/// description, keeping the rule text <see cref="OpenApiRuleParameterOperationTransformer"/> moved
/// there, so it may be registered before or after it.
/// </summary>
internal sealed class XmlDocumentationParameterOperationTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        if (operation.Parameters is not { Count: > 0 } parameters)
        {
            return Task.CompletedTask;
        }

        foreach (ApiParameterDescription description in context.Description.ParameterDescriptions)
        {
            if (FindProperty(description.ParameterDescriptor) is not { } property
                || XmlDocumentation.PropertySummary(property) is not { } summary)
            {
                continue;
            }

            OpenApiParameter? parameter = parameters
                .OfType<OpenApiParameter>()
                .FirstOrDefault(candidate => string.Equals(candidate.Name, description.Name, StringComparison.Ordinal));
            if (parameter is not null)
            {
                IReadOnlyList<string> rules = parameter.Schema is OpenApiSchema schema ? RuleText.Read(schema) : [];
                parameter.Description = XmlDocumentationSchemaTransformer.WithRules(summary, rules);
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// The property an <c>[AsParameters]</c> parameter is bound to: ASP.NET Core surfaces it as a
    /// <see cref="ParameterInfo"/> whose member is the property itself or, for a
    /// constructor-bound type, the type declaring a property of the same name.
    /// </summary>
    private static PropertyInfo? FindProperty(Microsoft.AspNetCore.Mvc.Abstractions.ParameterDescriptor? descriptor) =>
        descriptor is IParameterInfoParameterDescriptor { ParameterInfo: { } parameterInfo }
            ? parameterInfo.Member switch
            {
                PropertyInfo property => property,
                Type type when parameterInfo.Name is { } name => type.GetProperty(name),
                _ => null,
            }
            : null;
}
