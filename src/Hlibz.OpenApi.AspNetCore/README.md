![Hlibz.OpenApi.AspNetCore icon](https://raw.githubusercontent.com/hlib-zinchenko/Hlibz.OpenApi/main/docs/icon.png)

# Hlibz.OpenApi.AspNetCore

[![NuGet](https://img.shields.io/nuget/v/Hlibz.OpenApi.AspNetCore.svg)](https://www.nuget.org/packages/Hlibz.OpenApi.AspNetCore)
[![CI](https://github.com/hlib-zinchenko/Hlibz.OpenApi/actions/workflows/ci.yml/badge.svg)](https://github.com/hlib-zinchenko/Hlibz.OpenApi/actions/workflows/ci.yml)

Transformers for Microsoft.AspNetCore.OpenApi (`AddOpenApi()` / `MapOpenApi()`) that fill the gaps
between what your API checks and what its OpenAPI document says.

```bash
dotnet add package Hlibz.OpenApi.AspNetCore
```

```csharp
using Hlibz.OpenApi;

builder.Services.AddOpenApi("v1", options => options
    .AddXmlDocumentation()    // <summary>s as descriptions (enum values too), internal types and packages included
    .AddOpenApiRules()        // custom validation attributes describe themselves (IOpenApiRule), record parameters too
    .AddStronglyTypedIds()    // CountryId(Guid Value) → { "type": "string", "format": "uuid" }
    .AddValidationProblems()  // the 400 AddValidation() answers with, where it can
    .AddBearerSecurity()      // a lock on exactly the endpoints that require authorization
    .AddDescriptionCleanup()); // "Gets or sets the name." → "The name."
```

Several documents, one per endpoint group (`WithGroupName(...)`), configured once:

```csharp
IReadOnlyList<OpenApiDocumentDefinition> documents = builder.Services.AddOpenApiDocuments(
    [new("locations", "Locations"), new("locations-admin", "LocationsAdmin", "Locations (Admin)")],
    (services, name, configure) => services.AddOpenApi(name, configure),
    options => options.AddBearerSecurity());
```

`AddXmlDocumentation()` reads the XML file next to each assembly at runtime, so turn on
`GenerateDocumentationFile` in every project whose types you want described, and
`CopyDocumentationFilesFromPackages` in the app for types from NuGet packages. To keep IDs as
named components for existing clients: `AddStronglyTypedIds(ids => ids.AsComponents = true)`.

Trimming- and AOT-compatible. The document is built again on every request, so cache it:
`app.MapOpenApi().CacheOutput()` (with `AddOutputCache()` and `UseOutputCache()`).

On top of these transformers,
[Hlibz.OpenApi.Results](https://www.nuget.org/packages/Hlibz.OpenApi.Results) publishes
Hlibz.Results' typed errors,
[Hlibz.OpenApi.UseCases](https://www.nuget.org/packages/Hlibz.OpenApi.UseCases) Hlibz.UseCases'
rules and validation `400`s, and
[Hlibz.OpenApi.Paging](https://www.nuget.org/packages/Hlibz.OpenApi.Paging) Hlibz.Paging's sort
strings.

Targets .NET 11. .NET 10 apps stay on 1.x.

## A family of packages

One of five packages I build to set up new .NET APIs the way I want them: Hlibz.Results,
Hlibz.UseCases, Hlibz.MinimalApi.Endpoints, Hlibz.OpenApi and Hlibz.Paging. Each works on its own,
and they work best together, as in [Hlibz.Stack](https://github.com/hlib-zinchenko/Hlibz.Stack). New
minor and major versions come often, a new major whenever .NET gives me a better way to do
something. Breaking changes only ever come in a major and are listed first in its
[release notes](https://github.com/hlib-zinchenko/Hlibz.OpenApi/releases). You're welcome to use the
packages, and to [open an issue](https://github.com/hlib-zinchenko/Hlibz.OpenApi/issues) when
something is wrong or missing.

Full documentation and samples: [github.com/hlib-zinchenko/Hlibz.OpenApi](https://github.com/hlib-zinchenko/Hlibz.OpenApi).
