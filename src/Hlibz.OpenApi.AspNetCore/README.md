# Hlibz.OpenApi.AspNetCore

Transformers for Microsoft.AspNetCore.OpenApi (`AddOpenApi()` / `MapOpenApi()`), for .NET 10
and 11.

```csharp
using Hlibz.OpenApi;

builder.Services.AddOpenApi("v1", options => options
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

For XML `<summary>` comments, reference `Microsoft.AspNetCore.OpenApi` directly in the app and
turn on `GenerateDocumentationFile`: the comments come from a source generator in that package,
which doesn't flow through this one.

Full documentation: https://github.com/hlib-zinchenko/Hlibz.OpenApi
