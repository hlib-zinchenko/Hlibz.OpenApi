using System.Collections.Concurrent;
using System.Reflection;

namespace Hlibz.OpenApi.Internal;

/// <summary>
/// One document's rules on types, members and parameters: the <see cref="IOpenApiRule"/>
/// attributes (<see cref="RuleAttributes"/>) plus whatever the document's
/// <see cref="OpenApiRuleOptions.Sources"/> read, each read once.
/// </summary>
/// <param name="options">The document's rule options.</param>
internal sealed class RuleReader(OpenApiRuleOptions options)
{
    private readonly ConcurrentDictionary<object, IOpenApiRule[]> _cache = new();

    /// <summary>Gets the rules on a type (inherited ones included).</summary>
    /// <param name="type">The type.</param>
    /// <returns>The rules; empty when there are none.</returns>
    public IOpenApiRule[] ForType(Type type) =>
        WithSources(type, RuleAttributes.ForType(type), type);

    /// <summary>
    /// Gets the rules on whatever <paramref name="provider"/> is: a property or field from
    /// System.Text.Json's metadata, a type or a parameter.
    /// </summary>
    /// <param name="provider">The attribute provider.</param>
    /// <returns>The rules; empty when there are none.</returns>
    public IOpenApiRule[] For(ICustomAttributeProvider provider) =>
        provider switch
        {
            Type type => ForType(type),
            ParameterInfo parameter => ForParameter(parameter),
            _ => WithSources(provider, RuleAttributes.For(provider), provider),
        };

    /// <summary>
    /// Gets the rules on a handler or constructor parameter. Keyed by its member and position,
    /// since <see cref="ParameterInfo"/> instances aren't guaranteed to be the same object twice.
    /// </summary>
    /// <param name="parameter">The parameter.</param>
    /// <returns>The rules; empty when there are none.</returns>
    public IOpenApiRule[] ForParameter(ParameterInfo parameter) =>
        WithSources((parameter.Member, parameter.Position), RuleAttributes.ForParameter(parameter), parameter);

    /// <summary>
    /// Gets the rules on a positional record's constructor parameter: its own rules, and its
    /// DataAnnotations attributes (<see cref="ParameterAnnotations"/>).
    /// </summary>
    /// <param name="parameter">The constructor parameter behind a property.</param>
    /// <returns>The rules; empty when there are none.</returns>
    public IEnumerable<IOpenApiRule> ForRecordParameter(ParameterInfo parameter) =>
        ForParameter(parameter).Concat(ParameterAnnotations.For(parameter));

    private IOpenApiRule[] WithSources(object key, IOpenApiRule[] attributes, ICustomAttributeProvider provider)
    {
        if (options.Sources.Count == 0)
        {
            return attributes;
        }

        return _cache.GetOrAdd(
            key,
            static (_, state) =>
                [.. state.Attributes.Concat(state.Sources.SelectMany(source => source(state.Provider))).Distinct()],
            (Attributes: attributes, Sources: options.Sources, Provider: provider));
    }
}
