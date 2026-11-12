using Hlibz.OpenApi.Internal;

namespace Hlibz.OpenApi;

/// <summary>Registers description cleanup on an OpenAPI document.</summary>
public static class DescriptionCleanupOptionsExtensions
{
    /// <summary>
    /// Removes StyleCop's accessor wording ("Gets or sets ", "Gets ") from schema, property and
    /// parameter descriptions, which Microsoft.AspNetCore.OpenApi copies from XML
    /// <c>&lt;summary&gt;</c> comments as written: "Gets or sets the most rows to return." reads
    /// "The most rows to return.". Operation summaries and descriptions are left alone, since
    /// "Gets the order." is a fine sentence for an endpoint.
    /// </summary>
    /// <param name="options">The document's options.</param>
    /// <param name="configure">Changes the wording removed.</param>
    /// <returns>The same <paramref name="options"/>, for chaining.</returns>
    public static OpenApiOptions AddDescriptionCleanup(
        this OpenApiOptions options,
        Action<DescriptionCleanupOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(options);

        DescriptionCleanupOptions cleanup = new();
        configure?.Invoke(cleanup);

        return options.AddDocumentTransformer(
            new DescriptionCleanupDocumentTransformer([.. cleanup.Prefixes]));
    }
}
