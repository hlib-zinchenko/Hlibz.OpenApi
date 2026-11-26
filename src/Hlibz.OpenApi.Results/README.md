![Hlibz.OpenApi.Results icon](https://raw.githubusercontent.com/hlib-zinchenko/Hlibz.OpenApi/main/docs/icon.png)

# Hlibz.OpenApi.Results

[![NuGet](https://img.shields.io/nuget/vpre/Hlibz.OpenApi.Results.svg)](https://www.nuget.org/packages/Hlibz.OpenApi.Results)
[![CI](https://github.com/hlib-zinchenko/Hlibz.OpenApi/actions/workflows/ci.yml/badge.svg)](https://github.com/hlib-zinchenko/Hlibz.OpenApi/actions/workflows/ci.yml)

Publishes [Hlibz.Results](https://www.nuget.org/packages/Hlibz.Results.AspNetCore)' typed errors
in OpenAPI documents built with Microsoft.AspNetCore.OpenApi, for .NET 11: every error an
endpoint declares, with its code and the schema of its data.

```bash
dotnet add package Hlibz.OpenApi.Results
```

```csharp
using Hlibz.OpenApi;

[Error("atlas_1005", ResultStatus.Conflict, "The country still has cities.")]
public sealed record CountryHasCities(IReadOnlyList<CityId> CityIds) : Error;

public union DeleteCountryError(CountryNotFound, CountryHasCities);

builder.Services.AddResultErrorResponses();
builder.Services.AddOpenApi("v1", options => options
    .AddXmlDocumentation()   // Hlibz.OpenApi.AspNetCore, which this package brings
    .AddResults());

app.MapGroup("api/admin")
    .WithResultResponses()
    .MapDelete("countries/{id}", DeleteCountry); // returns Result<Unit, DeleteCountryError>
```

- Each declared error gets its own body component (`CountryHasCitiesErrorResponse`): the
  `ErrorResponse` with `code` narrowed to the error's code and `details` to the error's schema,
  described by the error's message.
- Several errors on one status code are a `oneOf` with a `discriminator` on `code`.
- Under `WithResultEnvelopes()` the bodies narrow `ResultEnvelope` instead.
- A status code the endpoint documents itself is left as it is, and so are the ambient errors
  (`ValidationFailed`, `Unauthorized`, `Forbidden`, `Unavailable`), which any endpoint can answer
  with.

Full documentation: [github.com/hlib-zinchenko/Hlibz.OpenApi](https://github.com/hlib-zinchenko/Hlibz.OpenApi#typed-errors).
