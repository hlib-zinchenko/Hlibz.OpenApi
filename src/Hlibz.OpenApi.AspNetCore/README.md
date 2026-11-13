![Hlibz.OpenApi.AspNetCore icon](https://raw.githubusercontent.com/hlib-zinchenko/Hlibz.OpenApi/main/docs/icon.png)

# Hlibz.OpenApi.AspNetCore

[![NuGet](https://img.shields.io/nuget/v/Hlibz.OpenApi.AspNetCore.svg)](https://www.nuget.org/packages/Hlibz.OpenApi.AspNetCore)
[![CI](https://github.com/hlib-zinchenko/Hlibz.OpenApi/actions/workflows/ci.yml/badge.svg)](https://github.com/hlib-zinchenko/Hlibz.OpenApi/actions/workflows/ci.yml)

Transformers for Microsoft.AspNetCore.OpenApi (`AddOpenApi()` / `MapOpenApi()`), for .NET 10
and 11.

```bash
dotnet add package Hlibz.OpenApi.AspNetCore
```

```csharp
using Hlibz.OpenApi;

builder.Services.AddOpenApi("v1", options => options
    .AddXmlDocumentation()    // <summary>s as descriptions, internal types and packages included
    .AddOpenApiRules()        // custom validation attributes describe themselves (IOpenApiRule)
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

## Related packages

- [Hlibz.MinimalApi.Endpoints](https://www.nuget.org/packages/Hlibz.MinimalApi.Endpoints):
  minimal API endpoints as classes, each class's `<summary>` its operation description. The
  sample is built on it.
- [Hlibz.Results.AspNetCore](https://www.nuget.org/packages/Hlibz.Results.AspNetCore): one
  error body for minimal APIs and controllers.

Full documentation and a sample: [github.com/hlib-zinchenko/Hlibz.OpenApi](https://github.com/hlib-zinchenko/Hlibz.OpenApi).
