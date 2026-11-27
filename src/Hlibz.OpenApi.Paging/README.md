![Hlibz.OpenApi.Paging icon](https://raw.githubusercontent.com/hlib-zinchenko/Hlibz.OpenApi/main/docs/icon.png)

# Hlibz.OpenApi.Paging

[![NuGet](https://img.shields.io/nuget/v/Hlibz.OpenApi.Paging.svg)](https://www.nuget.org/packages/Hlibz.OpenApi.Paging)
[![CI](https://github.com/hlib-zinchenko/Hlibz.OpenApi/actions/workflows/ci.yml/badge.svg)](https://github.com/hlib-zinchenko/Hlibz.OpenApi/actions/workflows/ci.yml)

Publishes [Hlibz.Paging](https://www.nuget.org/packages/Hlibz.Paging) in OpenAPI documents built
with Microsoft.AspNetCore.OpenApi: a sort string's sortable properties, format and default order.

```bash
dotnet add package Hlibz.OpenApi.Paging
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

Full documentation and samples: [github.com/hlib-zinchenko/Hlibz.OpenApi](https://github.com/hlib-zinchenko/Hlibz.OpenApi#paging).
