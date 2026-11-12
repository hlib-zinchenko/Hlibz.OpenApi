# Samples

## Atlas.Api

A small geography API (countries and cities, in memory) that uses everything
Hlibz.OpenApi.AspNetCore does, on endpoints written with
[Hlibz.MinimalApi.Endpoints](https://www.nuget.org/packages/Hlibz.MinimalApi.Endpoints) and
documented with [Hlibz.Redoc.AspNetCore](https://www.nuget.org/packages/Hlibz.Redoc.AspNetCore).

```bash
dotnet run --project samples/Atlas.Api
```

Then open http://localhost:5290/api-docs. The picker in the corner switches between the two
documents. [`Atlas.Api.http`](Atlas.Api/Atlas.Api.http) has a request for every case below,
showing that the API enforces what its documents say.

| Feature | Where to look | Code |
|---|---|---|
| Several documents | **Atlas** holds `api/countries` and `api/cities`; **Atlas (Admin)** holds `api/admin`. The `/` redirect has no group name, so it's in neither. | `Documents`, `AddOpenApiDocuments` and the `ConfigureGroups` convention in `Program.cs` |
| Strongly-typed IDs | `CountryDto.id` and `GET api/countries/{id}`: `uuid` strings. `CityDto.id` and `GET api/cities/{id}`: `int32`. The `countryId` filter: nullable `uuid`. `countryIds`: an array of `uuid`s. No `CountryId` or `CityId` component. | `Domain/CountryId.cs`, `Domain/CityId.cs` |
| A rule with a keyword | `SuggestCityRequest.countryId`: `not: { const: "000…" }` and "Must not be the empty id.". `CreateCountryRequest.code`: `pattern: ^[A-Z]{2}$`. `borders` and the `codes`/`countryIds` filters: `uniqueItems`. | `Rules/` |
| A rule in words only | The cross-field check on the city search, in the operation's description (an `[AsParameters]` type has no schema). `CreateCountryRequest.continent`: "Antarctica has no countries.", next to the shared `Continent` component, which stays clean. | `[OpenApiRule]` in `Domain/Models.cs` |
| `x-rules` | Shown as "rules:" under each field, because the ReDoc page turns on `showExtensions` for it. | `MapReDocDocuments` in `Program.cs` |
| Validation problems | A `400` with `HttpValidationProblemDetails` on the city search, the suggestion and the two admin `POST`s, where `AddValidation()` can fail; none on `GET api/cities/{id}` or `GET api/countries/{id}`, whose ID parameters have nothing to check. No endpoint declares its own `400`. | `AddValidationProblems()` and `AddProblemDetails()` in `Program.cs` |
| Security | A lock on `POST api/cities/suggestions` (a `Member` policy) and on the admin endpoints, which offer "Bearer or ApiKey". No lock on the rest, or on `GET api/admin/status`, which opts out with `AllowAnonymous()`. | `AddBearerSecurity`, plus `AddSecurity("ApiKey", …)` for the admin document only |
| Description cleanup | Every property and parameter reads "The city's population…" and "Whether the city is…", though the summaries say "Gets or sets…". Operation summaries and descriptions keep their "Gets…". | `AddDescriptionCleanup()` |
| XML comments | Every schema, property and parameter description above comes from a `<summary>`, because the app references `Microsoft.AspNetCore.OpenApi` directly and has `GenerateDocumentationFile` on. Each operation's description is its endpoint class's `<summary>`, which Hlibz.MinimalApi.Endpoints publishes; notes for developers stay in `<remarks>`. | `Atlas.Api.csproj`, `samples/Directory.Build.props`, `Endpoints/` |

The rules aren't only documentation: `AddValidation()` enforces the same attributes, so the
requests in the `.http` file get the `400`s the documents describe, as
`application/problem+json`. Authentication is a stand-in
(`DemoAuthenticationHandler`) that accepts the credentials the documents name:
`Authorization: Bearer demo-user` or `Bearer demo-admin`, and `X-Api-Key: demo-admin-key`.

The sample targets net11.0 only, references the library project and gets the other two packages
from NuGet. It is never packed.
