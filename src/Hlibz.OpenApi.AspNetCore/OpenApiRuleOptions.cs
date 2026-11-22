using System.Reflection;

namespace Hlibz.OpenApi;

/// <summary>
/// Options for <see cref="OpenApiRuleOptionsExtensions.AddOpenApiRules(OpenApiOptions, Action{OpenApiRuleOptions})"/>.
/// </summary>
public sealed class OpenApiRuleOptions
{
    /// <summary>
    /// Gets the places rules come from besides <see cref="IOpenApiRule"/> attributes: each one
    /// reads the rules a type, property, field or parameter carries, such as attributes that
    /// describe themselves through another library's interface. Their rules are written exactly
    /// as <see cref="IOpenApiRule"/> attributes are. Each source is asked once per type, member
    /// and parameter, and its answer is kept for every document.
    /// </summary>
    public IList<Func<ICustomAttributeProvider, IEnumerable<IOpenApiRule>>> Sources { get; } = [];
}
