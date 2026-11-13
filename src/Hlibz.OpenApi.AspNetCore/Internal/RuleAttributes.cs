using System.Collections.Concurrent;
using System.Reflection;

namespace Hlibz.OpenApi.Internal;

/// <summary>
/// The <see cref="IOpenApiRule"/> attributes on types, members and parameters, read once each.
/// Schema and operation transformers run for every schema and operation of every document
/// generated, and <c>GetCustomAttributes</c> creates new attribute instances on every call, so
/// the rules are cached and shared (which is why a rule must not change itself in
/// <see cref="IOpenApiRule.Describe"/>).
/// </summary>
internal static class RuleAttributes
{
    private static readonly ConcurrentDictionary<Type, IOpenApiRule[]> TypeRules = new();

    private static readonly ConcurrentDictionary<MemberInfo, IOpenApiRule[]> MemberRules = new();

    private static readonly ConcurrentDictionary<(MemberInfo Member, int Position), IOpenApiRule[]>
        ParameterRules = new();

    /// <summary>Gets the rules on a type (inherited ones included).</summary>
    /// <param name="type">The type.</param>
    /// <returns>The rules; empty when there are none.</returns>
    public static IOpenApiRule[] ForType(Type type) =>
        TypeRules.GetOrAdd(type, static key => Read(key));

    /// <summary>
    /// Gets the rules on whatever <paramref name="provider"/> is: a property or field from
    /// System.Text.Json's metadata, cached when it is a member.
    /// </summary>
    /// <param name="provider">The attribute provider.</param>
    /// <returns>The rules; empty when there are none.</returns>
    public static IOpenApiRule[] For(ICustomAttributeProvider provider) =>
        provider switch
        {
            Type type => ForType(type),
            MemberInfo member => MemberRules.GetOrAdd(member, static key => Read(key)),
            ParameterInfo parameter => ForParameter(parameter),
            _ => Read(provider),
        };

    /// <summary>
    /// Gets the rules on a handler or constructor parameter. Keyed by its member and position,
    /// since <see cref="ParameterInfo"/> instances aren't guaranteed to be the same object twice.
    /// </summary>
    /// <param name="parameter">The parameter.</param>
    /// <returns>The rules; empty when there are none.</returns>
    public static IOpenApiRule[] ForParameter(ParameterInfo parameter) =>
        ParameterRules.GetOrAdd((parameter.Member, parameter.Position), static (_, value) => Read(value), parameter);

    private static IOpenApiRule[] Read(ICustomAttributeProvider provider) =>
        [.. provider.GetCustomAttributes(inherit: true).OfType<IOpenApiRule>()];
}
