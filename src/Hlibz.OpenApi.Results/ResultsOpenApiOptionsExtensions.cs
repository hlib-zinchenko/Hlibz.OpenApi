using Hlibz.OpenApi.Results.Internal;

namespace Hlibz.OpenApi;

/// <summary>Publishes Hlibz.Results' typed errors in an OpenAPI document.</summary>
public static class ResultsOpenApiOptionsExtensions
{
    /// <summary>
    /// Documents every error an endpoint declares by returning a <c>Result&lt;T, TError&gt;</c>
    /// (under <c>WithResultResponses()</c> or <c>WithResultEnvelopes()</c>), or with
    /// <c>ProducesErrorSet&lt;TErrorSet&gt;()</c>, as its own body
    /// schema: the error body (<see cref="ErrorResponse"/>, or <see cref="ResultEnvelope"/>) with
    /// <c>code</c> narrowed to the error's code and <c>details</c> to the error's own schema, as
    /// a component named after the error and the body (<c>CityInUseErrorResponse</c>), described
    /// by the error's message. A status code several errors are sent with gets a <c>oneOf</c> of
    /// them, with a discriminator on <c>code</c>. Such a status code also lists the ambient errors
    /// (<see cref="AmbientError"/>) the app sends with it (<see cref="ResultHttpOptions.StatusCodes"/>),
    /// which any endpoint can answer with: a declared <c>400</c> keeps <c>validation_failed</c>, a
    /// declared <c>403</c> the authorization gate's <c>forbidden</c>.
    /// </summary>
    /// <param name="options">The document's options.</param>
    /// <returns>The same <paramref name="options"/>, for chaining.</returns>
    /// <remarks>
    /// A response the endpoint documents itself (<c>Produces&lt;T&gt;(409)</c>) is left as it is,
    /// and so are a status code only ambient errors are sent with, an app's own envelope (<see cref="ResultHttpOptions.CreateEnvelope"/>), and errors sent
    /// as <c>200</c> (<see cref="ResultHttpOptions.EnvelopeAlwaysOk"/>).
    /// </remarks>
    public static OpenApiOptions AddResults(this OpenApiOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return options.AddOperationTransformer(new DeclaredErrorsOperationTransformer());
    }
}
