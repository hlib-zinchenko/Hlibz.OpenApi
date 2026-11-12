namespace Hlibz.OpenApi.Internal;

/// <summary>
/// Works out what a schema stands for when ASP.NET Core builds it for a parameter. The context
/// carries the parameter's description for every schema built under it, not just the
/// parameter's own: an <c>[AsParameters]</c> array's item schema has the same
/// <c>ParameterDescription</c> as the array. And a parameter bound through <c>TryParse</c> is
/// described as a <see cref="string"/>, so <c>JsonTypeInfo</c> doesn't name its real type.
/// </summary>
internal static class ParameterSchemas
{
    /// <summary>
    /// Gets the parameter type the schema stands for, when the schema is the parameter's own
    /// (not a nested one), or <see langword="null"/>.
    /// </summary>
    /// <param name="context">The schema being transformed.</param>
    /// <returns>The parameter's type, or <see langword="null"/>.</returns>
    public static Type? OwnType(OpenApiSchemaTransformerContext context) =>
        Resolve(context) is (Type type, true) ? type : null;

    /// <summary>
    /// Gets the type a parameter schema stands for: the parameter's own type for its own schema,
    /// or the element type for the item schema of a collection parameter. <see langword="null"/>
    /// for a property schema, or one that isn't a parameter's.
    /// </summary>
    /// <param name="context">The schema being transformed.</param>
    /// <returns>The type, or <see langword="null"/>.</returns>
    public static Type? BoundType(OpenApiSchemaTransformerContext context) =>
        Resolve(context)?.Type;

    /// <summary>
    /// Gets the item type of a collection type (an array, or any <see cref="IEnumerable{T}"/>
    /// but <see cref="string"/>), or <see langword="null"/>.
    /// </summary>
    /// <param name="type">The type to check.</param>
    /// <returns>The item type, or <see langword="null"/>.</returns>
    public static Type? ElementType(Type type)
    {
        if (type == typeof(string))
        {
            return null;
        }

        if (type.IsArray)
        {
            return type.GetElementType();
        }

        return Array.Find(
                type.GetInterfaces().Append(type).ToArray(),
                candidate => candidate.IsGenericType
                             && candidate.GetGenericTypeDefinition() == typeof(IEnumerable<>))
            ?.GetGenericArguments()[0];
    }

    private static (Type Type, bool IsOwn)? Resolve(OpenApiSchemaTransformerContext context)
    {
        if (context.JsonPropertyInfo is not null || context.ParameterDescription?.Type is not { } parameterType)
        {
            return null;
        }

        Type declared = Unwrap(parameterType);
        Type described = Unwrap(context.JsonTypeInfo.Type);
        if (described == declared)
        {
            return (declared, true);
        }

        if (ElementType(declared) is not { } element)
        {
            // A TryParse-bound parameter is described as a string; anything else is nested
            // inside the parameter's type.
            return described == typeof(string) ? (declared, true) : null;
        }

        if (ElementType(described) is not null)
        {
            return (declared, true);
        }

        return described == Unwrap(element) || described == typeof(string)
            ? (Unwrap(element), false)
            : null;
    }

    private static Type Unwrap(Type type) => Nullable.GetUnderlyingType(type) ?? type;
}
