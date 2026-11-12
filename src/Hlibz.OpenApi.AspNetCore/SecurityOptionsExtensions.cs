using Hlibz.OpenApi.Internal;

namespace Hlibz.OpenApi;

/// <summary>Registers a security scheme, and where it applies, on an OpenAPI document.</summary>
public static class SecurityOptionsExtensions
{
    /// <summary>
    /// Documents HTTP bearer authentication (<c>Authorization: Bearer {token}</c>): adds the
    /// scheme to the document and marks every endpoint that requires authorization with it.
    /// </summary>
    /// <param name="options">The document's options.</param>
    /// <param name="configure">Sets the scheme's name, token format and description.</param>
    /// <returns>The same <paramref name="options"/>, for chaining.</returns>
    public static OpenApiOptions AddBearerSecurity(
        this OpenApiOptions options,
        Action<BearerSecurityOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(options);

        BearerSecurityOptions bearer = new();
        configure?.Invoke(bearer);

        return options.AddSecurity(
            bearer.SchemeName,
            new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = bearer.BearerFormat,
                Description = bearer.Description,
            });
    }

    /// <summary>
    /// Adds <paramref name="scheme"/> (an API key, OAuth 2, OpenID Connect, ...) to the document
    /// and marks every endpoint that requires authorization with it.
    /// </summary>
    /// <remarks>
    /// An endpoint requires authorization when it has authorization metadata
    /// (<c>[Authorize]</c>, <c>RequireAuthorization(...)</c>), or when the app sets
    /// <c>AuthorizationOptions.FallbackPolicy</c>, and isn't anonymous (<c>[AllowAnonymous]</c>,
    /// <c>AllowAnonymous()</c>). Register several schemes and each one is listed as an
    /// alternative.
    /// </remarks>
    /// <param name="options">The document's options.</param>
    /// <param name="name">The scheme's key in <c>components.securitySchemes</c>.</param>
    /// <param name="scheme">The scheme.</param>
    /// <returns>The same <paramref name="options"/>, for chaining.</returns>
    public static OpenApiOptions AddSecurity(
        this OpenApiOptions options,
        string name,
        OpenApiSecurityScheme scheme)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(scheme);

        return options
            .AddDocumentTransformer(new SecuritySchemeDocumentTransformer(name, scheme))
            .AddOperationTransformer(new SecurityRequirementOperationTransformer(name));
    }
}
