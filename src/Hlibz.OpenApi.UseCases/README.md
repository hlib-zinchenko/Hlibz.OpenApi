![Hlibz.OpenApi.UseCases icon](https://raw.githubusercontent.com/hlib-zinchenko/Hlibz.OpenApi/main/docs/icon.png)

# Hlibz.OpenApi.UseCases

[![NuGet](https://img.shields.io/nuget/vpre/Hlibz.OpenApi.UseCases.svg)](https://www.nuget.org/packages/Hlibz.OpenApi.UseCases)
[![CI](https://github.com/hlib-zinchenko/Hlibz.OpenApi/actions/workflows/ci.yml/badge.svg)](https://github.com/hlib-zinchenko/Hlibz.OpenApi/actions/workflows/ci.yml)

Publishes [Hlibz.UseCases](https://www.nuget.org/packages/Hlibz.UseCases) in OpenAPI documents
built with Microsoft.AspNetCore.OpenApi, for .NET 10 and 11: what each use case checks, and the
`400` an invalid one is answered with.

```bash
dotnet add package Hlibz.OpenApi.UseCases --prerelease
```

```csharp
using Hlibz.OpenApi;

builder.Services.AddUseCases([typeof(CreateCityCommand).Assembly]);
builder.Services.AddOpenApi("v1", options => options
    .AddXmlDocumentation()   // Hlibz.OpenApi.AspNetCore, which this package brings
    .AddUseCases(useCases => useCases.ValidationResponseType = typeof(ErrorResponse)));
```

- Every `IDescribedRule` (a custom validation attribute) and `[RuleSummary]` becomes schema
  keywords plus a sentence in `description` and `x-rules`, on the property, record parameter,
  parameter or type it sits on, through Hlibz.OpenApi.AspNetCore's `AddOpenApiRules()`.
- DataAnnotations on positional record parameters (`record CreateCityCommand([MaxLength(60)] string Name)`)
  are published too: Microsoft.AspNetCore.OpenApi reads properties only.
- An operation whose handler takes the handler of a use case that can fail validation gets a
  `400` (`HttpValidationProblemDetails`, or the type you set), unless it documents one already.

Rules reach the document only on types the endpoint binds: bind the use case itself, or give it a
record that is also the body.

Full documentation: [github.com/hlib-zinchenko/Hlibz.OpenApi](https://github.com/hlib-zinchenko/Hlibz.OpenApi#use-cases).
