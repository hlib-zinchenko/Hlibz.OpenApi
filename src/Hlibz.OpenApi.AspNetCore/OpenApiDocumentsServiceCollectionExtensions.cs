using Microsoft.Extensions.DependencyInjection;

namespace Hlibz.OpenApi;

/// <summary>Registers several OpenAPI documents, one per endpoint group.</summary>
public static class OpenApiDocumentsServiceCollectionExtensions
{
    /// <summary>
    /// Registers one OpenAPI document per definition, each holding only the endpoints of its
    /// group, and applies <paramref name="configureOptions"/> to all of them.
    /// </summary>
    /// <remarks>
    /// Unlike <c>AddOpenApi(name)</c> on its own, an endpoint with no group name is in none of
    /// the documents rather than in all of them. To change that, or anything else per document,
    /// set it in <paramref name="configureOptions"/>, which runs after the group filter and can
    /// read <see cref="OpenApiOptions.DocumentName"/>.
    /// </remarks>
    /// <param name="services">The services to register the documents on.</param>
    /// <param name="documents">The documents, in the order a document picker should list them.</param>
    /// <param name="addOpenApi">
    /// Your own <c>AddOpenApi</c> call: <c>(services, name, configure) =&gt;
    /// services.AddOpenApi(name, configure)</c>. It has to be written in your project, because
    /// Microsoft.AspNetCore.OpenApi publishes XML <c>&lt;summary&gt;</c> comments by intercepting
    /// the <c>AddOpenApi</c> calls in the compiled app; a call made inside this package would
    /// register documents without them.
    /// </param>
    /// <param name="configureOptions">Configures every document, e.g. adds transformers.</param>
    /// <returns>
    /// The documents registered, to hand on, e.g. to ReDoc:
    /// <c>documents.Select(d =&gt; new ReDocDocument(d.Name, d.Title))</c>.
    /// </returns>
    /// <exception cref="ArgumentException">Two documents share a name.</exception>
    public static IReadOnlyList<OpenApiDocumentDefinition> AddOpenApiDocuments(
        this IServiceCollection services,
        IEnumerable<OpenApiDocumentDefinition> documents,
        Func<IServiceCollection, string, Action<OpenApiOptions>, IServiceCollection> addOpenApi,
        Action<OpenApiOptions>? configureOptions = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(documents);
        ArgumentNullException.ThrowIfNull(addOpenApi);

        OpenApiDocumentDefinition[] definitions = [.. documents];
        string? duplicate = definitions
            .GroupBy(document => document.Name, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1)
            ?.Key;
        if (duplicate is not null)
        {
            throw new ArgumentException(
                $"More than one OpenAPI document is named '{duplicate}'.",
                nameof(documents));
        }

        foreach (OpenApiDocumentDefinition document in definitions)
        {
            addOpenApi(services, document.Name, options =>
            {
                string groupName = document.EffectiveGroupName;
                options.ShouldInclude = description =>
                    string.Equals(description.GroupName, groupName, StringComparison.Ordinal);
                if (document.Title is { } title)
                {
                    options.AddDocumentTransformer((openApiDocument, _, _) =>
                    {
                        openApiDocument.Info ??= new OpenApiInfo();
                        openApiDocument.Info.Title = title;
                        return Task.CompletedTask;
                    });
                }

                configureOptions?.Invoke(options);
            });
        }

        return definitions;
    }
}
