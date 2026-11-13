namespace Hlibz.OpenApi;

/// <summary>Options for <see cref="StronglyTypedIdOptionsExtensions.AddStronglyTypedIds"/>.</summary>
public sealed class StronglyTypedIdOptions
{
    /// <summary>
    /// Gets or sets which types are IDs: it returns the type an ID type wraps
    /// (<c>typeof(Guid)</c>, <c>typeof(int)</c>, ...), or <see langword="null"/> for a type that
    /// isn't an ID. Defaults to <see langword="null"/>, which matches a struct whose only public
    /// property is <c>Value</c>, serialized as a single value by a converter.
    /// </summary>
    public Func<Type, Type?>? ValueTypeOf { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether each ID stays a component schema of its own
    /// (<c>CountryId</c>: <c>{ "type": "string", "format": "uuid" }</c>) that properties point at
    /// with a <c>$ref</c>, as without this feature. Defaults to <see langword="false"/>: IDs are
    /// inlined. Turn it on when generated clients already use the ID types' names. A property's
    /// rules are then described next to its <c>$ref</c>, as for any other component.
    /// </summary>
    public bool AsComponents { get; set; }
}
