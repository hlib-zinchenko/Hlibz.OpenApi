![Hlibz.OpenApi icon](https://raw.githubusercontent.com/hlib-zinchenko/Hlibz.OpenApi/main/docs/icon.png)

# Hlibz.OpenApi

[![CI](https://github.com/hlib-zinchenko/Hlibz.OpenApi/actions/workflows/ci.yml/badge.svg)](https://github.com/hlib-zinchenko/Hlibz.OpenApi/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://github.com/hlib-zinchenko/Hlibz.OpenApi/blob/main/LICENSE)<br>
[![Hlibz.OpenApi.AspNetCore](https://img.shields.io/nuget/v/Hlibz.OpenApi.AspNetCore?label=Hlibz.OpenApi.AspNetCore)](https://www.nuget.org/packages/Hlibz.OpenApi.AspNetCore)
[![downloads](https://img.shields.io/nuget/dt/Hlibz.OpenApi.AspNetCore?label=downloads)](https://www.nuget.org/packages/Hlibz.OpenApi.AspNetCore)

Transformers for [Microsoft.AspNetCore.OpenApi](https://learn.microsoft.com/aspnet/core/fundamentals/openapi/overview)
(`AddOpenApi()` / `MapOpenApi()`) that fill the gaps between what your API checks and what its
OpenAPI document says.

| Feature | Call | What it does |
|---|---|---|
| [XML documentation](#xml-documentation) | `AddXmlDocumentation()` | `<summary>` comments as descriptions, internal types and packages included. |
| [Self-describing rules](#self-describing-validation-rules) | `AddOpenApiRules()` | Custom validation attributes write their own schema keywords and text. |
| [Strongly-typed IDs](#strongly-typed-ids) | `AddStronglyTypedIds()` | `CountryId(Guid Value)` is published as `string`/`uuid`, not an object. |
| [Validation problems](#validation-problems) | `AddValidationProblems()` | The `400` that `AddValidation()` answers with, on exactly the operations it can come from. |
| [Security](#security) | `AddBearerSecurity()`, `AddSecurity(...)` | A lock on exactly the endpoints that require authorization. |
| [Description cleanup](#description-cleanup) | `AddDescriptionCleanup()` | "Gets or sets the name." reads "The name.". |
| [Several documents](#several-documents) | `services.AddOpenApiDocuments(...)` | One document per endpoint group, configured once. |

Targets .NET 10 and 11.

```bash
dotnet add package Hlibz.OpenApi.AspNetCore
```

```csharp
using Hlibz.OpenApi;

builder.Services.AddOpenApi("v1", options => options
    .AddXmlDocumentation()
    .AddOpenApiRules()
    .AddStronglyTypedIds()
    .AddValidationProblems()
    .AddBearerSecurity()
    .AddDescriptionCleanup());

app.MapOpenApi();
```

## XML documentation

`AddXmlDocumentation()` publishes XML `<summary>` comments as the descriptions of schemas, their
properties and `[AsParameters]` parameters. It reads them at runtime from the XML file next to
each assembly, so turn the file on in every project whose types you want described:

```xml
<GenerateDocumentationFile>true</GenerateDocumentationFile>
```

```csharp
/// <summary>Suggests a city for a <see cref="CountryDto"/>.</summary>
internal sealed class SuggestCityRequest
{
    /// <summary>Gets or sets the city's name, as it's written locally.</summary>
    public required string Name { get; init; }
}
```

Microsoft.AspNetCore.OpenApi has an XML comment generator of its own, which runs at compile time.
On a real API it misses a lot:

| | Its generator | `AddXmlDocumentation()` |
|---|---|---|
| `internal` types (request bodies, `[AsParameters]` queries) | skipped on .NET 10 (.NET 11 includes them) | described |
| Types from other projects and NuGet packages | skipped | described, from their XML files (packages': see below) |
| `<see cref="City.Name"/>` | "a `string` City.Name" | "City.Name" |
| A summary over several lines | keeps the line breaks | one line |
| Runs when | the app references Microsoft.AspNetCore.OpenApi (directly, or through this package) and calls `AddOpenApi(name, ...)` itself | always |

- A record's `<param>` tag describes its positional property.
- A property whose schema becomes a component (a DTO, an enum) gets its summary next to its
  `$ref`, so it never lands on the component every other use shares.
- `<remarks>` are never read: keep notes for developers there.
- It replaces the generator's descriptions wherever it has a summary, so both can be on. The
  generator still writes operation descriptions from handler methods' summaries;
  `AddXmlDocumentation()` leaves operations alone. (Endpoint classes from
  [Hlibz.MinimalApi.Endpoints](https://www.nuget.org/packages/Hlibz.MinimalApi.Endpoints) describe
  their operations themselves.)
- The rule text `AddOpenApiRules()` writes stays after the summary, whichever is registered first.

To describe types from NuGet packages too (an error body from a shared package, say), have the
build copy each package's XML file next to its DLL, in the app's project. `dotnet publish`
then ships them as well:

```xml
<CopyDocumentationFilesFromPackages>true</CopyDocumentationFilesFromPackages>
```

## Self-describing validation rules

DataAnnotations' own attributes (`[Required]`, `[MaxLength]`, `[Range]`, `[RegularExpression]`)
already reach the schema. A custom `ValidationAttribute` doesn't: the API enforces it, but the
document never mentions it. Implement `IOpenApiRule` and the attribute describes itself:

```csharp
public sealed class StrongPasswordAttribute : ValidationAttribute, IOpenApiRule
{
    public override bool IsValid(object? value) =>
        value is string p && p.Any(char.IsUpper) && p.Any(char.IsLower) && p.Any(char.IsDigit);

    public void Describe(OpenApiSchema schema)
    {
        schema.Pattern = "^(?=.*[a-z])(?=.*[A-Z])(?=.*\\d).*$";
        RuleText.Add(schema, "Must contain a lowercase and an uppercase letter and a digit.");
    }
}

public sealed class ChangePassword
{
    /// <summary>The user's new password.</summary>
    [StrongPassword, MinLength(8)]
    public string Password { get; set; } = "";
}
```

```json
"password": {
  "minLength": 8,
  "pattern": "^(?=.*[a-z])(?=.*[A-Z])(?=.*\\d).*$",
  "type": "string",
  "description": "The user's new password.\n\nMust contain a lowercase and an uppercase letter and a digit.",
  "x-rules": ["Must contain a lowercase and an uppercase letter and a digit."]
}
```

- **Use a keyword when there is one.** If JSON Schema has a keyword for the rule (`pattern`,
  `not`, `uniqueItems`, `exclusiveMinimum`), set it, so client generators can enforce it. Add a
  sentence as well: a lookahead regex says little to a person.
- **`RuleText.Add`** appends the sentence to `description` (what ReDoc and Swagger UI show) as its
  own paragraph, and to `x-rules` (a list for tools). Adding the same sentence twice does nothing.
- **Where it works:** body properties, `[AsParameters]` properties and handler parameters. For a
  parameter, the sentence also goes in the parameter's own `description`, which is what UIs show
  for parameters. On an array parameter it describes the array, not its items.
- **Rules with no attribute of their own**, such as a cross-field check in
  `IValidatableObject.Validate`, take `[OpenApiRule("...")]` on the type (or on a property or
  parameter):

  ```csharp
  [OpenApiRule("End must not be earlier than Start.")]
  public sealed record DateRange(DateOnly Start, DateOnly End) : IValidatableObject { ... }
  ```

  An `[AsParameters]` type has no schema of its own, so its type-level rules go in the
  operation's description instead.

- **A property whose type is a component schema** (an enum, a DTO) is published as a `$ref`, and
  every other use shares that component. The rule's sentence goes next to the `$ref`, where the
  XML summary goes too. The rule's keywords are dropped, so they don't end up on the shared
  component.

The attributes still validate as usual, for example under .NET 10's `AddValidation()`.

## Strongly-typed IDs

```csharp
[JsonConverter(typeof(CountryIdConverter))]   // writes the Guid itself
public readonly record struct CountryId(Guid Value);

builder.Services.AddOpenApi("v1", options => options.AddStronglyTypedIds());
```

Without this, an ID with a JSON converter gets an empty schema, and a route parameter is a bare
`string`. With it:

```json
"countryId":         { "type": "string", "format": "uuid" },
"previousCountryId": { "type": ["null", "string"], "format": "uuid" }
```

- The ID takes the schema of the type it wraps, so `int`, `long` and `string` IDs work too.
- IDs are inlined, not made into components, and `CountryId?` stays nullable. To keep the
  components, see below.
- Route and query parameters (`MapGet("countries/{id}", (CountryId id) => ...)`) are covered,
  and so are arrays of them in the query string (`?countryIds=...&countryIds=...`).
- The default rule matches a struct whose only public property is `Value` and which
  System.Text.Json writes as a single value. A struct with no converter is written as
  `{ "value": ... }`, which is what the API really sends, so it's left alone.

To use your own rule, return the wrapped type, or `null` for anything else:

```csharp
options.AddStronglyTypedIds(ids =>
    ids.ValueTypeOf = type => type.IsAssignableTo(typeof(IEntityId)) ? typeof(Guid) : null);
```

A rule attribute on an ID property works as on any other property. For example, a `[NotDefault]`
that writes `not: { const: "00000000-..." }` ends up on the inlined ID.

If generated clients already use the IDs' names (a `CountryId` type in TypeScript, say), keep
each ID a component of its own, now with the wrapped value's schema:

```csharp
options.AddStronglyTypedIds(ids => ids.AsComponents = true);
```

```json
"countryId":         { "$ref": "#/components/schemas/CountryId" },
"previousCountryId": { "oneOf": [{ "type": "null" }, { "$ref": "#/components/schemas/CountryId" }] },
"CountryId":         { "type": "string", "format": "uuid" }
```

Parameters stay inline. A rule on an ID property is then described next to its `$ref` in words
only, as on any other component property.

## Validation problems

.NET's validation (`builder.Services.AddValidation()`) checks minimal API parameters and answers
an invalid request with `400` and an `HttpValidationProblemDetails` body (an `errors` map from
field to messages). It doesn't add that response to the operation. `AddValidationProblems()`
does:

```csharp
builder.Services.AddValidation();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi("v1", options => options.AddValidationProblems());
```

```json
"400": {
  "description": "The request didn't pass validation.",
  "content": {
    "application/problem+json": {
      "schema": { "$ref": "#/components/schemas/HttpValidationProblemDetails" }
    }
  }
}
```

It goes on **exactly the operations that can answer with it**:

- ASP.NET Core validates the endpoint: `AddValidation()` is registered, the endpoint doesn't call
  `DisableValidation()`, and a handler parameter that isn't a service is one the validation
  covers. The transformer asks `ValidationOptions` the same question ASP.NET Core does.
- **and** that parameter has a rule to break: a validation attribute on it, on its type, or on a
  property or record parameter inside it, or an `IValidatableObject`. The validation generator
  also registers types with nothing to check, such as `record struct CityId(int Value)`, and
  validating those never fails.

An operation that already documents a `400` (`ProducesValidationProblem()`,
`ProducesProblem(400)`, `Produces<T>(400)`) is left as it is.

The content type is the one the app answers with: `application/problem+json` when it calls
`AddProblemDetails()` (or registers another `IProblemDetailsService`), `application/json` when it
doesn't. If an `IProblemDetailsWriter` writes another shape, say so:

```csharp
options.AddValidationProblems(problems =>
{
    problems.ResponseType = typeof(ErrorResponse);
    problems.ContentType = "application/json";
    problems.Description = "One or more fields are invalid.";
});
```

## Security

```csharp
builder.Services.AddOpenApi("v1", options => options.AddBearerSecurity(bearer =>
{
    bearer.Description = "The access token from `POST /auth/login`.";
}));
```

This adds an HTTP bearer scheme to the document (named `Bearer`, format `JWT`; both can be
changed). It also adds a security requirement to **exactly the endpoints that require
authorization**, so ReDoc and Swagger UI put a lock on them. An endpoint requires authorization
when:

- it has authorization metadata (`[Authorize]`, `RequireAuthorization(...)`, on the endpoint or
  its group), **or** the app sets `AuthorizationOptions.FallbackPolicy`;
- **and** nothing makes it anonymous (`[AllowAnonymous]`, `AllowAnonymous()`).

Other schemes (API key, OAuth 2, OpenID Connect) go through `AddSecurity`. If you register
several, each one is listed as an alternative:

```csharp
options
    .AddBearerSecurity()
    .AddSecurity("ApiKey", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.ApiKey,
        In = ParameterLocation.Header,
        Name = "X-Api-Key",
    });
```

## Description cleanup

StyleCop wants property summaries to start with "Gets or sets ...", and XML summaries are
copied as written. `AddDescriptionCleanup()` removes that wording from schema, property and
parameter descriptions:

| Summary | Description |
|---|---|
| Gets or sets the most rows to return. | The most rows to return. |
| Gets the city's population. | The city's population. |
| Gets or sets a value indicating whether the order is paid. | Whether the order is paid. |

Operation summaries are left alone ("Gets the order." is a fine sentence for an endpoint). The
wording it removes is a list you can change:

```csharp
options.AddDescriptionCleanup(cleanup => cleanup.Prefixes.Add("Represents "));
```

## Several documents

On its own, `AddOpenApi(name)` puts every endpoint without a group name into every document.
`AddOpenApiDocuments` registers one document per endpoint group, applies one configure callback to
all of them, and returns the list it registered:

```csharp
IReadOnlyList<OpenApiDocumentDefinition> documents = builder.Services.AddOpenApiDocuments(
    [
        new("locations", GroupName: "Locations"),
        new("locations-admin", GroupName: "LocationsAdmin", Title: "Locations (Admin)"),
    ],
    (services, name, configure) => services.AddOpenApi(name, configure),
    options => options.AddOpenApiRules().AddStronglyTypedIds().AddBearerSecurity());

app.MapGroup("cities").WithGroupName("Locations")...;
app.MapGroup("admin/cities").WithGroupName("LocationsAdmin")...;
```

- Each document holds only the endpoints whose group name is its `GroupName`. `GroupName`
  defaults to the document name. Endpoints without a group name are in none of the documents.
  Controllers set the group with `[ApiExplorerSettings(GroupName = "...")]`.
- `Title` sets `info.title`.
- The `addOpenApi` callback is your own `AddOpenApi` call, written in your app so
  Microsoft.AspNetCore.OpenApi's XML comment generator can see it (it never rewrites a call made
  inside a library). Copy it as shown.
- The configure callback runs after the group filter, so it can override `ShouldInclude`. It can
  tell the documents apart by `options.DocumentName`.
- Two documents with the same name throw an exception at startup.

The returned list can feed a document picker, such as the one in
[Hlibz.Redoc.AspNetCore](https://www.nuget.org/packages/Hlibz.Redoc.AspNetCore):

```csharp
app.MapReDocDocuments([.. documents.Select(document => new ReDocDocument(document.Name, document.Title))]);
```

## Performance and AOT

- **Cache the document.** Microsoft.AspNetCore.OpenApi builds the document again on every
  request to `MapOpenApi()`'s endpoint, running every transformer. The transformers here read
  each type's attributes and XML file once and keep them, but the document itself is the app's
  to cache: `builder.Services.AddOutputCache()`, `app.UseOutputCache()`,
  `app.MapOpenApi().CacheOutput()`, as the sample does.
- **Rules are shared.** Each `IOpenApiRule` attribute is read once and the same instance
  describes every schema it sits on, so `Describe` writes to the schema only.
- **Trimming and Native AOT.** The package is marked AOT-compatible. It reflects only on types
  ASP.NET Core hands it (parameter, property and validatable types), whose members the app's
  own binding, serialization and generated validation keep. `AddStronglyTypedIds`' default rule
  reads an ID's `Value` property; a `ValueTypeOf` of your own doesn't reflect at all.

## Limitations

- **.NET 10 and 11 only.** .NET 9's transformers use Microsoft.OpenApi 1.x, an object model too
  different to share code with the 2.x and 3.x that .NET 10 and 11 use.
- **XML documentation needs the XML files on disk.** `AddXmlDocumentation()` reads each one next
  to its assembly; a single-file publish has none to read, so its types go undescribed.
- **Swashbuckle.** Everything here plugs into Microsoft.AspNetCore.OpenApi's transformers.
  Swashbuckle uses filters instead, so a port would be a separate `Hlibz.OpenApi.Swashbuckle`
  package.

## Sample

[`samples/Atlas.Api`](samples/README.md) is a small geography API that uses every feature above,
on endpoints written with [Hlibz.MinimalApi.Endpoints](https://www.nuget.org/packages/Hlibz.MinimalApi.Endpoints)
and rendered by [Hlibz.Redoc.AspNetCore](https://www.nuget.org/packages/Hlibz.Redoc.AspNetCore):
a public and an admin document, IDs, rules, validation `400`s, locks and cleaned-up
descriptions, with a `.http` file showing the API enforces what the documents say.

```bash
dotnet run --project samples/Atlas.Api   # then open http://localhost:5290/api-docs
```

## Related packages

- [Hlibz.MinimalApi.Endpoints](https://www.nuget.org/packages/Hlibz.MinimalApi.Endpoints):
  minimal API endpoints as classes, grouped and checked at build time. The sample's endpoints
  use it, and each class's `<summary>` becomes its operation description.
- [Hlibz.Results.AspNetCore](https://www.nuget.org/packages/Hlibz.Results.AspNetCore): one error
  body for minimal APIs and controllers.
- [Hlibz.Paging](https://www.nuget.org/packages/Hlibz.Paging): paging and sorting for list
  endpoints.

## Versioning

Every package in this repository is released under one version, from one `vX.Y.Z` tag.
Microsoft.AspNetCore.OpenApi is declared as a minimum for each target framework (10.0.14 on
.NET 10, 11.0.0 on .NET 11). A newer patch of it needs no new release here: reference it in your
app and NuGet uses it.

Release notes: [GitHub Releases](https://github.com/hlib-zinchenko/Hlibz.OpenApi/releases).

## License

MIT
