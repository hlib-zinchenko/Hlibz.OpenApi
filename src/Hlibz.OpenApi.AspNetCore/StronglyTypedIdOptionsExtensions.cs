using Hlibz.OpenApi.Internal;

namespace Hlibz.OpenApi;

/// <summary>Registers strongly-typed ID support on an OpenAPI document.</summary>
public static class StronglyTypedIdOptionsExtensions
{
    /// <summary>
    /// Publishes strongly-typed IDs as the value they wrap: <c>readonly record struct
    /// CountryId(Guid Value)</c>, written by a JSON converter as a plain GUID, becomes
    /// <c>{ "type": "string", "format": "uuid" }</c> rather than an object with a <c>value</c>
    /// property. By default IDs are inlined, not made into components of their own, and their
    /// nullable form stays nullable; <see cref="StronglyTypedIdOptions.AsComponents"/> keeps each
    /// a component.
    /// </summary>
    /// <param name="options">The document's options.</param>
    /// <param name="configure">Changes which types are IDs, and whether they stay components.</param>
    /// <returns>The same <paramref name="options"/>, for chaining.</returns>
    /// <example>
    /// <code>
    /// options.AddStronglyTypedIds();
    /// options.AddStronglyTypedIds(ids =>
    /// {
    ///     ids.ValueTypeOf = type => type.IsAssignableTo(typeof(IEntityId)) ? typeof(Guid) : null;
    ///     ids.AsComponents = true;
    /// });
    /// </code>
    /// </example>
    public static OpenApiOptions AddStronglyTypedIds(
        this OpenApiOptions options,
        Action<StronglyTypedIdOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(options);

        StronglyTypedIdOptions ids = new();
        configure?.Invoke(ids);

        return options.AddSchemaTransformer(new StronglyTypedIdSchemaTransformer(ids.ValueTypeOf, ids.AsComponents));
    }
}
