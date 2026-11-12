namespace Hlibz.OpenApi.Internal;

/// <summary>Adds a security scheme to the document's components.</summary>
/// <param name="name">The scheme's key in <c>components.securitySchemes</c>.</param>
/// <param name="scheme">The scheme.</param>
internal sealed class SecuritySchemeDocumentTransformer(string name, OpenApiSecurityScheme scheme)
    : IOpenApiDocumentTransformer
{
    public Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??=
            new Dictionary<string, IOpenApiSecurityScheme>(StringComparer.Ordinal);
        document.Components.SecuritySchemes[name] = scheme;

        return Task.CompletedTask;
    }
}
