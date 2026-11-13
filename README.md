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
    .AddOpenApiRules()
    .AddStronglyTypedIds()
    .AddValidationProblems()
    .AddBearerSecurity()
    .AddDescriptionCleanup());

app.MapOpenApi();
```

## XML comments

Microsoft.AspNetCore.OpenApi already turns XML `<summary>` comments into descriptions, at compile
time. It needs three things this package can't do for you:

- `<GenerateDocumentationFile>true</GenerateDocumentationFile>` in the app, and in any project
  whose types you want described.
- A **direct** `PackageReference` to `Microsoft.AspNetCore.OpenApi` in the app. The comments come
  from a source generator in that package. Analyzers don't flow through a transitive reference, so
  if you only get the package through this one, you get no comments.
- The `AddOpenApi(...)` call written in the app, as `AddOpenApi(name, options => ...)` or
  `AddOpenApi(name, configure)`. The generator rewrites those calls. It doesn't rewrite
  `AddOpenApi(configure)` with a delegate *variable*, and it never rewrites a call made from
  inside a library.

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
- IDs are inlined, not made into components, and `CountryId?` stays nullable.
- Route and query parameters (`MapGet("countries/{id}", (CountryId id) => ...)`) are covered,
  and so are arrays of them in the query string (`?countryIds=...&countryIds=...`).
- The default rule matches a struct whose only public property is `Value` and which
  System.Text.Json writes as a single value. A struct with no converter is written as
  `{ "value": ... }`, which is what the API really sends, so it's left alone.

To use your own rule, return the wrapped type, or `null` for anything else:

```csharp
options.AddStronglyTypedIds(type => type.IsAssignableTo(typeof(IEntityId)) ? typeof(Guid) : null);
```

A rule attribute on an ID property works as on any other property. For example, a `[NotDefault]`
that writes `not: { const: "00000000-..." }` ends up on the inlined ID.

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

StyleCop wants property summaries to start with "Gets or sets ...", and the XML comment generator
copies them as written. `AddDescriptionCleanup()` removes that wording from schema, property and
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
- The `addOpenApi` callback is your own `AddOpenApi` call, written in your app so the XML comment
  generator can see it (see [XML comments](#xml-comments)). Copy it as shown.
- The configure callback runs after the group filter, so it can override `ShouldInclude`. It can
  tell the documents apart by `options.DocumentName`.
- Two documents with the same name throw an exception at startup.

The returned list can feed a document picker, such as the one in
[Hlibz.Redoc.AspNetCore](https://www.nuget.org/packages/Hlibz.Redoc.AspNetCore):

```csharp
app.MapReDocDocuments([.. documents.Select(document => new ReDocDocument(document.Name, document.Title))]);
```

## Limitations

- **.NET 10 and 11 only.** .NET 9's transformers use Microsoft.OpenApi 1.x, an object model too
  different to share code with the 2.x and 3.x that .NET 10 and 11 use.
- **XML comments need a direct reference.** They come from a source generator in
  Microsoft.AspNetCore.OpenApi, which only runs in a project that references that package
  itself (see [XML comments](#xml-comments)).
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
