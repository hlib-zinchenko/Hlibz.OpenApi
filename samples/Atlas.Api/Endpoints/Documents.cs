using Hlibz.OpenApi;

namespace Atlas.Api.Endpoints;

/// <summary>
/// The sample's OpenAPI documents, one per endpoint group name. Program.cs registers them with
/// <c>AddOpenApiDocuments</c> and hands the same list to ReDoc's picker.
/// </summary>
public static class Documents
{
    /// <summary>The endpoint group name of every public group.</summary>
    public const string PublicGroup = "Locations";

    /// <summary>The endpoint group name of every <see cref="IAdminEndpointGroup"/>.</summary>
    public const string AdminGroup = "LocationsAdmin";

    public static readonly OpenApiDocumentDefinition Public =
        new("locations", PublicGroup, "Atlas");

    public static readonly OpenApiDocumentDefinition Admin =
        new("locations-admin", AdminGroup, "Atlas (Admin)");

    public static IReadOnlyList<OpenApiDocumentDefinition> All { get; } = [Public, Admin];
}
