![Hlibz.OpenApi.Paging icon](https://raw.githubusercontent.com/hlib-zinchenko/Hlibz.OpenApi/main/docs/icon.png)

# Hlibz.OpenApi.Paging

[![NuGet](https://img.shields.io/nuget/vpre/Hlibz.OpenApi.Paging.svg)](https://www.nuget.org/packages/Hlibz.OpenApi.Paging)
[![CI](https://github.com/hlib-zinchenko/Hlibz.OpenApi/actions/workflows/ci.yml/badge.svg)](https://github.com/hlib-zinchenko/Hlibz.OpenApi/actions/workflows/ci.yml)

Publishes [Hlibz.Paging](https://www.nuget.org/packages/Hlibz.Paging) in OpenAPI documents
built with Microsoft.AspNetCore.OpenApi, for .NET 10 and 11: a sort string's sortable
properties, format and default order.

```bash
dotnet add package Hlibz.OpenApi.Paging --prerelease
```

```csharp
using Hlibz.OpenApi;

public sealed record GetOrdersQuery(
    [property: SortableBy(typeof(OrderDto), NamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower)]
    string? Sort);

builder.Services.AddOpenApi("v1", options => options.AddPaging());
```

```text
Sort by: id, customer_name, created_at. Comma-separated, each optionally followed by :asc
(the default) or :desc. Default: created_at:desc.
```

- The sentence goes in the `description` and `x-rules` of the property, record parameter or
  parameter `[SortableBy]` sits on, through Hlibz.OpenApi.AspNetCore's `AddOpenApiRules()`, and
  into a query parameter's own description.
- The default order is the schema's `default`.
- Properties are named as the attribute's errors name them (`NamingPolicy`).

Full documentation: [github.com/hlib-zinchenko/Hlibz.OpenApi](https://github.com/hlib-zinchenko/Hlibz.OpenApi#paging).
