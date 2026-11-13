using System.Reflection;

using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace Hlibz.OpenApi.Internal;

/// <summary>
/// Puts rules where ReDoc and Swagger UI show them for parameters. The sentences
/// <see cref="OpenApiRuleSchemaTransformer"/> wrote into a parameter's schema move up to the
/// parameter's own description; the schema keeps its keywords and its <c>x-rules</c> list. And
/// the rules on an <c>[AsParameters]</c> type, which has no schema of its own, go in the
/// operation's description.
/// </summary>
internal sealed class OpenApiRuleParameterOperationTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        foreach (OpenApiParameter parameter in (operation.Parameters ?? []).OfType<OpenApiParameter>())
        {
            if (parameter.Schema is not OpenApiSchema schema
                || RuleText.Read(schema) is not { Count: > 0 } rules)
            {
                continue;
            }

            foreach (string rule in rules)
            {
                parameter.Description = RuleText.Append(parameter.Description, rule);
                schema.Description = RuleText.Remove(schema.Description, rule);
            }
        }

        OpenApiSchema scratch = new();
        foreach (IOpenApiRule rule in AsParametersTypeRules(context.Description.ParameterDescriptions))
        {
            rule.Describe(scratch);
        }

        foreach (string text in RuleText.Read(scratch))
        {
            operation.Description = RuleText.Append(operation.Description, text);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Gets the rules on the types the operation's <c>[AsParameters]</c> parameters come from.
    /// ASP.NET Core describes each of their properties as a parameter whose
    /// <see cref="ParameterInfo.Member"/> is the property or, for a constructor-bound type, the
    /// type itself.
    /// </summary>
    private static IEnumerable<IOpenApiRule> AsParametersTypeRules(
        IEnumerable<ApiParameterDescription> parameters) =>
        parameters
            .Select(parameter => parameter.ParameterDescriptor)
            .OfType<IParameterInfoParameterDescriptor>()
            .Select(descriptor => descriptor.ParameterInfo.Member switch
            {
                PropertyInfo property => property.ReflectedType,
                Type type => type,
                _ => null,
            })
            .OfType<Type>()
            .Distinct()
            .SelectMany(RuleAttributes.ForType);
}
