![Hlibz.OpenApi.UseCases icon](https://raw.githubusercontent.com/hlib-zinchenko/Hlibz.OpenApi/main/docs/icon.png)

# Hlibz.OpenApi.UseCases

[![NuGet](https://img.shields.io/nuget/v/Hlibz.OpenApi.UseCases.svg)](https://www.nuget.org/packages/Hlibz.OpenApi.UseCases)
[![CI](https://github.com/hlib-zinchenko/Hlibz.OpenApi/actions/workflows/ci.yml/badge.svg)](https://github.com/hlib-zinchenko/Hlibz.OpenApi/actions/workflows/ci.yml)

Publishes [Hlibz.UseCases](https://www.nuget.org/packages/Hlibz.UseCases) in OpenAPI documents built
with Microsoft.AspNetCore.OpenApi: what each use case checks, and the `400` an invalid one is
answered with.

```bash
dotnet add package Hlibz.OpenApi.UseCases
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

Full documentation and samples: [github.com/hlib-zinchenko/Hlibz.OpenApi](https://github.com/hlib-zinchenko/Hlibz.OpenApi#use-cases).
