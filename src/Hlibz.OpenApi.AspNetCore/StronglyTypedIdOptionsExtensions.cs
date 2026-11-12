using Hlibz.OpenApi.Internal;

namespace Hlibz.OpenApi;

/// <summary>Registers strongly-typed ID support on an OpenAPI document.</summary>
public static class StronglyTypedIdOptionsExtensions
{
    /// <summary>
    /// Publishes strongly-typed IDs as the value they wrap: <c>readonly record struct
    /// CountryId(Guid Value)</c>, written by a JSON converter as a plain GUID, becomes
    /// <c>{ "type": "string", "format": "uuid" }</c> rather than an object with a <c>value</c>
    /// property. IDs are inlined, not made into components of their own, and their nullable form
    /// stays nullable.
    /// </summary>
    /// <param name="options">The document's options.</param>
    /// <param name="valueTypeOf">
    /// Gets the type an ID type wraps (<c>typeof(Guid)</c>, <c>typeof(int)</c>, ...), or
    /// <see langword="null"/> for a type that isn't an ID. Leave it out for the default: a struct
    /// whose only public property is <c>Value</c>, serialized as a single value by a converter.
    /// </param>
    /// <returns>The same <paramref name="options"/>, for chaining.</returns>
    /// <example>
    /// <code>
    /// options.AddStronglyTypedIds();
    /// options.AddStronglyTypedIds(type => type.IsAssignableTo(typeof(IEntityId)) ? typeof(Guid) : null);
    /// </code>
    /// </example>
    public static OpenApiOptions AddStronglyTypedIds(
        this OpenApiOptions options,
        Func<Type, Type?>? valueTypeOf = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        return options.AddSchemaTransformer(new StronglyTypedIdSchemaTransformer(valueTypeOf));
    }
}
