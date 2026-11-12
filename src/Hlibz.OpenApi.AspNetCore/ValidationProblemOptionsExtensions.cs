using Hlibz.OpenApi.Internal;

namespace Hlibz.OpenApi;

/// <summary>Documents the <c>400</c> that .NET's validation (<c>AddValidation()</c>) answers with.</summary>
public static class ValidationProblemOptionsExtensions
{
    /// <summary>
    /// Adds a <c>400</c> response with an <c>HttpValidationProblemDetails</c> body to every
    /// minimal API operation that <c>AddValidation()</c> validates: one with a parameter its
    /// validation covers (a <c>[ValidatableType]</c> body, a validated <c>[AsParameters]</c>
    /// type, a parameter with a validation attribute), unless the endpoint calls
    /// <c>DisableValidation()</c>. ASP.NET Core answers an invalid request to those with that
    /// <c>400</c> but leaves it out of the document.
    /// </summary>
    /// <remarks>
    /// An operation that already documents a <c>400</c> (<c>ProducesValidationProblem()</c>,
    /// <c>ProducesProblem(400)</c>, <c>Produces&lt;T&gt;(400)</c>) is left as it is. Without
    /// <c>AddValidation()</c> no operation is validated, so none changes.
    /// </remarks>
    /// <param name="options">The document's options.</param>
    /// <param name="configure">Changes the response's body type, content type and description.</param>
    /// <returns>The same <paramref name="options"/>, for chaining.</returns>
    public static OpenApiOptions AddValidationProblems(
        this OpenApiOptions options,
        Action<ValidationProblemOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(options);

        ValidationProblemOptions problems = new();
        configure?.Invoke(problems);
        ArgumentNullException.ThrowIfNull(problems.ResponseType);

        return options.AddOperationTransformer(new ValidationProblemOperationTransformer(
            problems.ResponseType,
            problems.ContentType,
            problems.Description));
    }
}
