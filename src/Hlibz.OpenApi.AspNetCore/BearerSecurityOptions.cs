namespace Hlibz.OpenApi;

/// <summary>Options for <see cref="SecurityOptionsExtensions.AddBearerSecurity"/>.</summary>
public sealed class BearerSecurityOptions
{
    /// <summary>
    /// Gets or sets the scheme's key in <c>components.securitySchemes</c>. Defaults to
    /// <c>"Bearer"</c>.
    /// </summary>
    public string SchemeName { get; set; } = "Bearer";

    /// <summary>
    /// Gets or sets the token format shown next to the scheme. Defaults to <c>"JWT"</c>;
    /// <see langword="null"/> leaves it out.
    /// </summary>
    public string? BearerFormat { get; set; } = "JWT";

    /// <summary>
    /// Gets or sets the scheme's description, e.g. where to get a token. Markdown is allowed.
    /// </summary>
    public string? Description { get; set; }
}
