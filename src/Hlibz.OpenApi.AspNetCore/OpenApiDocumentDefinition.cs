namespace Hlibz.OpenApi;

/// <summary>
/// One OpenAPI document registered by
/// <see cref="OpenApiDocumentsServiceCollectionExtensions.AddOpenApiDocuments"/>, holding the
/// endpoints of one group.
/// </summary>
/// <param name="Name">
/// The document name, as in <c>AddOpenApi(name)</c> and its URL (<c>/openapi/{name}.json</c>),
/// e.g. <c>"locations-admin"</c>.
/// </param>
/// <param name="GroupName">
/// The endpoint group name the document holds, as set by <c>WithGroupName(...)</c> (minimal APIs)
/// or <c>[ApiExplorerSettings(GroupName = ...)]</c> (controllers), e.g. <c>"LocationsAdmin"</c>.
/// Defaults to <paramref name="Name"/>.
/// </param>
/// <param name="Title">
/// The document's title (<c>info.title</c>), also a good label for a document picker such as
/// ReDoc's, e.g. <c>"Locations (Admin)"</c>. Defaults to ASP.NET Core's own title.
/// </param>
public sealed record OpenApiDocumentDefinition(
    string Name,
    string? GroupName = null,
    string? Title = null)
{
    /// <summary>
    /// Gets the endpoint group name the document holds: <see cref="GroupName"/>, or
    /// <see cref="Name"/> when that isn't set.
    /// </summary>
    public string EffectiveGroupName => GroupName ?? Name;
}
