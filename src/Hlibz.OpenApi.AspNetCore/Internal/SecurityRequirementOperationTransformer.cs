using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Hlibz.OpenApi.Internal;

/// <summary>
/// Marks every operation whose endpoint requires authorization with a security requirement for
/// one scheme, so ReDoc and Swagger UI show a lock on exactly those endpoints.
/// </summary>
/// <param name="name">The scheme's key in <c>components.securitySchemes</c>.</param>
internal sealed class SecurityRequirementOperationTransformer(string name)
    : IOpenApiOperationTransformer
{
    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        AuthorizationPolicy? fallbackPolicy = context
            .ApplicationServices
            .GetService<IOptions<AuthorizationOptions>>()
            ?.Value
            .FallbackPolicy;
        if (!RequiresAuthorization(context.Description.ActionDescriptor.EndpointMetadata, fallbackPolicy))
        {
            return Task.CompletedTask;
        }

        OpenApiSecuritySchemeReference reference = new(name, context.Document, externalResource: null);
        operation.Security ??= [];
        if (operation.Security.Any(existing => existing.Keys.Any(key => key.Reference.Id == name)))
        {
            return Task.CompletedTask;
        }

        operation.Security.Add(new OpenApiSecurityRequirement { [reference] = [] });
        return Task.CompletedTask;
    }

    /// <summary>
    /// Whether an endpoint ends up requiring authorization: it carries authorization metadata
    /// (<c>[Authorize]</c>, <c>RequireAuthorization(...)</c>) or the app has a fallback policy,
    /// and nothing makes it anonymous (<c>[AllowAnonymous]</c>, <c>AllowAnonymous()</c>).
    /// </summary>
    /// <param name="metadata">The endpoint's metadata.</param>
    /// <param name="fallbackPolicy">The app's fallback policy, if any.</param>
    /// <returns>Whether a request needs to be authorized.</returns>
    internal static bool RequiresAuthorization(
        IList<object> metadata,
        AuthorizationPolicy? fallbackPolicy) =>
        !metadata.Any(item => item is IAllowAnonymous)
        && (fallbackPolicy is not null
            || metadata.Any(item => item is IAuthorizeData or AuthorizationPolicy));
}
