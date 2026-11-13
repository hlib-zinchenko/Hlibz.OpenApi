using Hlibz.OpenApi.Internal;

namespace Hlibz.OpenApi;

/// <summary>Registers XML documentation on an OpenAPI document.</summary>
public static class XmlDocumentationOptionsExtensions
{
    /// <summary>
    /// Publishes XML <c>&lt;summary&gt;</c> comments as schema, property and <c>[AsParameters]</c>
    /// parameter descriptions, read at runtime from the XML file next to each assembly. Unlike
    /// Microsoft.AspNetCore.OpenApi's own XML comment generator, it covers <c>internal</c> types
    /// and types from other projects and packages, writes a <c>&lt;see cref&gt;</c> as the name it
    /// refers to and puts each summary on one line. A record's <c>&lt;param&gt;</c> describes its
    /// positional property. It replaces the generator's descriptions where it has a summary;
    /// operation descriptions are left alone. Types from NuGet packages are described when the
    /// app sets <c>CopyDocumentationFilesFromPackages</c>, so their XML files are copied too.
    /// </summary>
    /// <param name="options">The document's options.</param>
    /// <returns>The same <paramref name="options"/>, for chaining.</returns>
    public static OpenApiOptions AddXmlDocumentation(this OpenApiOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return options
            .AddSchemaTransformer(new XmlDocumentationSchemaTransformer())
            .AddOperationTransformer(new XmlDocumentationParameterOperationTransformer());
    }
}
