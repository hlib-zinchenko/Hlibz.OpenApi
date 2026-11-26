using Atlas.Api.Domain;

using Hlibz.Results;
using Hlibz.Results.AspNetCore;

using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.Api.Endpoints;

/// <summary>
/// The public <c>api/countries</c> endpoints. Each handler's <c>&lt;summary&gt;</c> is its operation's
/// summary and its <c>&lt;remarks&gt;</c> the description, read by Microsoft.AspNetCore.OpenApi's
/// XML comment generator (from non-private methods only); notes for developers are comments.
/// </summary>
internal static class CountryEndpoints
{
    public static void MapCountries(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder countries = app.MapGroup("api/countries")
            .WithTags("Countries")
            .WithGroupName(Documents.PublicGroup)
            .WithResultResponses();

        countries.MapGet(string.Empty, GetCountries).WithName(nameof(GetCountries));
        countries.MapGet("{id}", GetCountry).WithName(nameof(GetCountry));
    }

    // Anonymous: no lock in the docs.

    /// <summary>Gets every country.</summary>
    /// <remarks>Gets every country in the atlas, sorted by name.</remarks>
    internal static Ok<IReadOnlyList<CountryDto>> GetCountries([FromServices] AtlasStore store) =>
        TypedResults.Ok(store.Countries);

    // The {id} route parameter is a CountryId, bound through its TryParse: AddStronglyTypedIds()
    // documents it as a uuid string, not the bare string ASP.NET Core would. The 404 is
    // CountryNotFound's own body, with the id in its details (AddResults()).

    /// <summary>Gets one country by its id.</summary>
    /// <remarks>An unknown id is a `404`.</remarks>
    internal static Result<CountryDto, GetCountryError> GetCountry(
        CountryId id,
        [FromServices] AtlasStore store) =>
        store.FindCountry(id) is { } country ? country : new CountryNotFound(id);
}
