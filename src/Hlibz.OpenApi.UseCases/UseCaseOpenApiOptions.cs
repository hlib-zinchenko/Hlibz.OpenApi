using Microsoft.AspNetCore.Http;

namespace Hlibz.OpenApi;

/// <summary>Options for <see cref="UseCaseOpenApiOptionsExtensions.AddUseCases"/>.</summary>
public sealed class UseCaseOpenApiOptions
{
    /// <summary>
    /// Gets or sets the type of the <c>400</c> body an invalid use case is answered with. Defaults to
    /// <see cref="HttpValidationProblemDetails"/>; set the one your app writes, such as Hlibz.Results'
    /// <c>ErrorResponse</c>.
    /// </summary>
    public Type ValidationResponseType { get; set; } = typeof(HttpValidationProblemDetails);

    /// <summary>
    /// Gets or sets the content type of the <c>400</c> body. Defaults to <see langword="null"/>:
    /// <c>application/problem+json</c> when the app registers an <c>IProblemDetailsService</c>,
    /// <c>application/json</c> otherwise.
    /// </summary>
    public string? ValidationContentType { get; set; }

    /// <summary>
    /// Gets or sets the <c>400</c>'s description. Defaults to "The request didn't pass validation.".
    /// </summary>
    public string ValidationDescription { get; set; } = "The request didn't pass validation.";
}
