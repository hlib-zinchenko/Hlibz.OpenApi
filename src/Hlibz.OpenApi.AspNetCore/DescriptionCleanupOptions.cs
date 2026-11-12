namespace Hlibz.OpenApi;

/// <summary>Options for <see cref="DescriptionCleanupOptionsExtensions.AddDescriptionCleanup"/>.</summary>
public sealed class DescriptionCleanupOptions
{
    /// <summary>
    /// Gets the wording removed from the start of a description, tried in order; the first that
    /// matches wins and the rest of the sentence is capitalized. Defaults to StyleCop's property
    /// wording: "Gets or sets a value indicating ", "Gets a value indicating ", "Gets or sets ",
    /// "Gets " (so "Gets or sets a value indicating whether the order is paid." reads "Whether
    /// the order is paid.").
    /// </summary>
    public IList<string> Prefixes { get; } =
    [
        "Gets or sets a value indicating ",
        "Gets a value indicating ",
        "Gets or sets ",
        "Gets ",
    ];
}
