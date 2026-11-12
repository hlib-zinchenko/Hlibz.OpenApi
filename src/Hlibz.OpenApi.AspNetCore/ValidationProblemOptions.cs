using Microsoft.AspNetCore.Http;

namespace Hlibz.OpenApi;

/// <summary>Options for <see cref="ValidationProblemOptionsExtensions.AddValidationProblems"/>.</summary>
public sealed class ValidationProblemOptions
{
    /// <summary>
    /// Gets or sets the type of the <c>400</c> body. Defaults to
    /// <see cref="HttpValidationProblemDetails"/>, what <c>AddValidation()</c> writes; set your
    /// own when an <c>IProblemDetailsWriter</c> writes another shape.
    /// </summary>
    public Type ResponseType { get; set; } = typeof(HttpValidationProblemDetails);

    /// <summary>
    /// Gets or sets the content type of the <c>400</c> body. Defaults to <see langword="null"/>,
    /// the one the app answers with: <c>application/problem+json</c> when it registers an
    /// <c>IProblemDetailsService</c> (<c>AddProblemDetails()</c>), <c>application/json</c>
    /// otherwise.
    /// </summary>
    public string? ContentType { get; set; }

    /// <summary>Gets or sets the response's description. Defaults to "The request didn't pass validation.".</summary>
    public string Description { get; set; } = "The request didn't pass validation.";
}
