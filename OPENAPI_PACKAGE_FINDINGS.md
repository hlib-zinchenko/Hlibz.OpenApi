# OpenAPI package: findings

Notes for the proposed `Hlibz.AspNetCore.OpenApi.Extensions` package, drawn from Lighthouse's
`src/Common/Lighthouse.Common.API/OpenApi` and from probes run on a scratch .NET 11 app
(SDK 11.0.100, `Microsoft.AspNetCore.OpenApi` 11.0.0). Written 2026-11-12.

Lighthouse targets **net10.0**. Everything below was checked on .NET 11 unless it says
otherwise; recheck the items marked *(recheck on .NET 10)* before relying on them there.

## TL;DR

The package is worth building, but smaller than proposed and around a different centre:

| Piece | Verdict |
|---|---|
| Self-describing validation attributes (`IOpenApiRule`) | **New, and the main reason for the package.** Generic, not in .NET. |
| Strongly-typed ID schema transformer | **Ship**, matching a predicate or "struct with a single `Value`", not `IEntityId`. |
| Bearer security scheme and requirement | **Ship, after fixing it**: the requirement ignores `AllowAnonymous`. Make the scheme configurable. |
| "Gets or sets" / "Gets" prefix stripping | **Ship** as a tiny description-cleanup transformer; the rest of the XML code goes. |
| XML documentation reader and transformers | ~~Drop: the built-in generator covers it.~~ **Ship** as `AddXmlDocumentation()`: it doesn't (see section 2). |
| `PagedDataSchemaTransformer` | **Drop**: `PagedData`/`PagingMetadata` properties are `required` from Hlibz.Paging 1.0.0-preview.2. |
| Module documents (`ApiModule`, `AddModuleOpenApiDocuments`) | **Split:** registering several documents goes in the OpenAPI package. ReDoc already takes the list. Endpoints only sets `WithGroupName`. Choosing the modules stays in Lighthouse. |
| `SortableParameterOperationTransformer` | **Keep in Lighthouse** (tied to `BaseGetQuery`). |
| Document the `400` of endpoints `AddValidation()` validates | **Done: `AddValidationProblems()`.** .NET doesn't document it. |

## 1. Custom validation attributes can describe themselves in OpenAPI

DataAnnotations' built-in attributes already reach the schema (see section 5), but a custom
`ValidationAttribute` is invisible. A small interface plus one schema transformer fixes that:
each attribute writes its own schema keywords and text.

```csharp
/// <summary>A validation attribute that can say what it checks in the OpenAPI schema.</summary>
public interface IOpenApiRule
{
    void Describe(OpenApiSchema schema);
}

public sealed class StrongPasswordAttribute : ValidationAttribute, IOpenApiRule
{
    public override bool IsValid(object? value) =>
        value is string p && p.Any(char.IsUpper) && p.Any(char.IsLower)
        && p.Any(char.IsDigit) && p.Any(c => !char.IsLetterOrDigit(c));

    public void Describe(OpenApiSchema schema)
    {
        schema.Pattern = "^(?=.*[a-z])(?=.*[A-Z])(?=.*\\d)(?=.*[^A-Za-z0-9]).*$";
        RuleText.Add(schema, "Must contain a lowercase and an uppercase letter, a digit and a symbol.");
    }
}

public sealed class NotDefaultAttribute : ValidationAttribute, IOpenApiRule
{
    public override bool IsValid(object? value) =>
        value is null || !value.Equals(Activator.CreateInstance(value.GetType()));

    public void Describe(OpenApiSchema schema)
    {
        schema.Not = new OpenApiSchema { Const = "00000000-0000-0000-0000-000000000000" };
        RuleText.Add(schema, "Must not be the empty id.");
    }
}

public static class RuleText
{
    /// <summary>Appends the rule to the description (rendered by ReDoc) and to x-rules.</summary>
    public static void Add(OpenApiSchema schema, string text)
    {
        schema.Description = string.IsNullOrEmpty(schema.Description)
            ? text
            : $"{schema.Description}\n\n{text}";
        schema.Extensions ??= new Dictionary<string, IOpenApiExtension>();
        JsonArray rules = schema.Extensions.TryGetValue("x-rules", out IOpenApiExtension? existing)
                          && existing is JsonNodeExtension { Node: JsonArray array }
            ? array
            : [];
        rules.Add(text);
        schema.Extensions["x-rules"] = new JsonNodeExtension(rules);
    }
}

/// <summary>Lets every IOpenApiRule attribute on a property or parameter describe its schema.</summary>
public sealed class RuleSchemaTransformer : IOpenApiSchemaTransformer
{
    public Task TransformAsync(
        OpenApiSchema schema,
        OpenApiSchemaTransformerContext context,
        CancellationToken cancellationToken)
    {
        IEnumerable<object> attributes =
            context.JsonPropertyInfo?.AttributeProvider?.GetCustomAttributes(inherit: true)
            ?? (context.ParameterDescription?.ParameterDescriptor
                    as IParameterInfoParameterDescriptor)?.ParameterInfo.GetCustomAttributes(inherit: true)
            ?? [];
        foreach (IOpenApiRule rule in attributes.OfType<IOpenApiRule>())
        {
            rule.Describe(schema);
        }

        return Task.CompletedTask;
    }
}

// builder.Services.AddOpenApi(options => options.AddSchemaTransformer<RuleSchemaTransformer>());
```

Tested output, for a JSON body property and for an `[AsParameters]` query property alike:

```json
"password": {
  "minLength": 8,
  "pattern": "^(?=.*[a-z])(?=.*[A-Z])(?=.*\\d)(?=.*[^A-Za-z0-9]).*$",
  "type": "string",
  "description": "The user's new password.\n\nMust contain a lowercase and an uppercase letter, a digit and a symbol.",
  "x-rules": ["Must contain a lowercase and an uppercase letter, a digit and a symbol."]
},
"countryId": {
  "type": "string",
  "format": "uuid",
  "not": { "const": "00000000-0000-0000-0000-000000000000" },
  "description": "Must not be the empty id.",
  "x-rules": ["Must not be the empty id."]
}
```

The same attributes still validate under `AddValidation()` (both returned `400` on bad input).

Design notes:

- Prefer a real JSON Schema keyword when one exists, so client generators can enforce it:
  `pattern` (password), `not: { const }` (non-default ids), `uniqueItems` (whole-item
  uniqueness), `exclusiveMinimum`. Add text as well, because a lookahead regex says little to a
  human.
- `description` is what ReDoc shows; `x-rules` is hidden unless ReDoc's `showExtensions` is on,
  but gives tools a list to read.
- Cross-field rules (`IValidatableObject`) have no property to sit on: a class-level
  `[Rule("End must not be earlier than Start.")]` could add text to the type's description.
- Not yet checked: a property whose type is a `$ref`'d component schema. Its own description may
  need the same `x-ref-description` handling Lighthouse's XML transformer has.

## 2. Built-in XML comments make most of Lighthouse's XML code redundant

> **Corrected (2026-11-13), after Lighthouse adopted 1.0.0-preview.1.** The verdict below was
> wrong. On Lighthouse the generator lost 193 schema and 67 parameter descriptions, because it
> skips `internal` types (every request body and `[AsParameters]` query). It never reads other
> assemblies' XML files (`ErrorResponse`, `ErrorCode` from Hlibz.Results), it writes a
> `<see cref>` with its member's type ("Who `UserId?` AdminContactMessageListItemDto.CreatedBy
> is") and loses the space after a `<see langword>`, and it keeps line breaks (83 descriptions).
> The runtime reader now ships as `AddXmlDocumentation()`. The original analysis follows: it only
> looked at public types in one project.

With `GenerateDocumentationFile` on, `Microsoft.AspNetCore.OpenApi`'s own source-generated
XML comment support already emitted:

- type summaries as schema descriptions (`Weights` → "How a household's costs are weighted.");
- property summaries on schema properties (`Weights.fixed` → "Gets the fixed weight.");
- `[AsParameters]` property summaries as query parameter descriptions (`Limit` → "Gets or sets
  the most rows to return."). *(recheck on .NET 10)*

What it doesn't do is remove StyleCop's accessor wording ("Gets or sets ", "Gets "). That is the
only thing left of `XmlDocumentation`, `XmlDocumentationSchemaTransformer` and
`XmlDocumentationParameterOperationTransformer` worth shipping, as a short transformer over
schema, parameter and operation descriptions. Drop the runtime XML-file loading.

Endpoint summaries as operation descriptions are now done by Hlibz.MinimalApi.Endpoints itself,
at compile time (`DescribeEndpointsFromSummaries`).

## 3. `PagedDataSchemaTransformer` can be replaced by `required`

> **Done (2026-11-13)** in Hlibz.Paging 1.0.0-preview.2: every `PagedData`/`PagingMetadata`
> property is `required` + `init`, with a parameterless constructor for serializers and the
> existing ones marked `[SetsRequiredMembers]`; checked against Microsoft.AspNetCore.OpenApi 10
> and 11.

The transformer only marks every property of `PagedData<T>`/`PagingMetadata` as required.
Tested: C#'s `required` modifier already puts a property in the schema's `required` list
(`public required int Total { get; init; }` → `"required": ["total", "items"]`; a non-`required`
property was left out). Mark those properties `required` in
`Lighthouse.Common.Core/Paging` and delete the transformer. *(recheck on .NET 10)*

## 4. What's worth shipping, and how

### Strongly-typed IDs

`StronglyTypedIdSchemaTransformer` rewrites any `IEntityId` to `string`/`uuid`. For a package,
match on a predicate (`options.IdTypes = type => ...`), with a default of "a struct with a single
`Value` property", and take the inner type's schema rather than hard-coding `uuid` (ids can wrap
`int`, `long`, `string`). It pairs with `[NotDefault]` from section 1.

### Bearer security

`BearerSecuritySchemeDocumentTransformer` plus `BearerSecurityRequirementOperationTransformer`.
Generic and useful, but the requirement transformer counts `IAuthorizeData` only, so an endpoint
with `AllowAnonymous` still gets a security requirement. Use "does the endpoint end up requiring
authorization": `IAuthorizeData` or `AuthorizationPolicy`, and no `IAllowAnonymous`. Hlibz.MinimalApi.Endpoints'
internal `ResponseDocumentation.RequiresAuthorization` makes the same check. The OpenAPI package
keeps its own copy of these few lines rather than referencing Endpoints, because each package is
used on its own (see "Package roles" below). Make the scheme configurable (name, bearer format,
other scheme types).

### Module documents: split them three ways

`AddModuleOpenApiDocuments` registers one `AddOpenApi(name)` per module, filtered by
`ShouldInclude = d => d.GroupName == module.Name`. `ApiModule` derives modules from Lighthouse's
`Lighthouse.<Module>.API` assembly names and from `EndpointDiscovery` (admin groups make a
second, admin document).

Package roles: each package works without the others.

- **ReDoc** renders whatever OpenAPI documents it is given. It is for a broad audience.
- **Hlibz OpenApi** handles transformers and schema work. It is for a broad audience.
- **Hlibz.MinimalApi.Endpoints** handles endpoints. It is a niche package for the author's own
  projects.

This feature splits into the parts below:

| Part | Where | Why |
|---|---|---|
| Register several documents. Each document holds only its own group name's endpoints. One configure callback applies to all of them. The result is a name/title list. | `Hlibz.AspNetCore.OpenApi.Extensions` (broad) | Any minimal API or controller app can use it, and it needs `Microsoft.AspNetCore.OpenApi`. |
| Give the documents to ReDoc's picker. | Already done: `MapReDocDocuments(list)` takes the list. | ReDoc doesn't reference `Microsoft.AspNetCore.OpenApi`. It also has a Swashbuckle flavour, so registering documents can't live there. |
| Tag each endpoint group with its document name. | Nothing to ship: `ConfigureGroups(ctx => ctx.Builder.WithGroupName(...))` already does it. | `WithGroupName` is in the shared framework, so Endpoints stays free of NuGet dependencies. |
| Choose modules: the assembly naming rule, plus an admin document for admin groups. | Lighthouse (`ApiModule`) | These are Lighthouse's own conventions. |

Why the broad helper is worth shipping:
- `AddOpenApi(name)` by default puts every endpoint without a group name into every document.
- Lighthouse's group names (`LocationsAdmin`) differ from its document names
  (`locations-admin`), so its document filter is custom.
- Lighthouse repeats seven transformer registrations for each document.

A helper that fixes these three things, and returns the list it registered, is useful outside
Lighthouse.

### Document the `400` of `AddValidation()`

Tested: `AddValidation()` validates and answers `400`, but the operation's `responses` listed
only `200`. An operation transformer in the OpenAPI package could add
`400 HttpValidationProblemDetails` to endpoints whose parameters the validation generator covers.
This belongs in the OpenAPI package, not Endpoints: it documents `AddValidation()` for any app.
The FluentValidation add-on documents its failure separately because it adds the filter itself.
Done as `AddValidationProblems()`. Tested: asking `ValidationOptions.TryGetValidatableParameterInfo`
for each non-service handler parameter (ASP.NET Core's own test) works, but isn't enough on its
own: the generator also registers record structs with no rules (strongly-typed IDs), so the
transformer also requires a validation attribute or `IValidatableObject` inside the parameter.
Without `AddProblemDetails()` the `400` is sent as `application/json`, not problem+json.

### Naming

`Hlibz.AspNetCore.OpenApi.Extensions` reads very close to Microsoft's
`Microsoft.AspNetCore.OpenApi`, and "Extensions" says little. `Hlibz.OpenApi.Rules` (if the
attribute rules lead) or `Hlibz.AspNetCore.OpenApi.Transformers` say what it does.

## 5. Background: what `AddValidation()` and DataAnnotations do (tested)

These shaped the verdicts above and the Lighthouse FluentValidation → DataAnnotations question.

**Schema keywords from built-in attributes**, on body properties and query parameters (including
`[AsParameters]` properties):

| Attribute | Schema | Enforced by `AddValidation()` |
|---|---|---|
| `[Required]` | `required` | ✅ |
| `[MaxLength]` / `[MinLength]` | `maxLength` / `minLength` | ✅ |
| `[Range]` | `minimum` / `maximum`; `MinimumIsExclusive = true` → `exclusiveMinimum` | ✅ |
| `[Range(typeof(decimal), "0", "1000000000000", ParseLimitsInInvariantCulture = true)]` | `exclusiveMinimum`/`maximum` | ✅ |
| `[RegularExpression]` | `pattern` | ✅ |
| `[EnumDataType(typeof(T))]` | the enum's schema | ✅ |
| `[EmailAddress]` | nothing (no `format: email`) | ✅ |
| Custom `ValidationAttribute`, `AsyncValidationAttribute`, `IValidatableObject` | nothing, unless the attribute implements `IOpenApiRule` | ✅ |

Other facts:

- Nested objects are validated recursively, and `null` ones are skipped (`[Required]` makes one
  mandatory). Error keys are paths: `Weights.Fixed`, `OrderItems[0].Description`.
- Handlers passed as method groups to `MapPost` inside a class are found by the validation
  source generator. That's the Hlibz.MinimalApi.Endpoints pattern.
- The generator only covers types in assemblies that call `AddValidation()` themselves, so each
  module API project needs its own call (and the `Microsoft.Extensions.Validation` package if
  it's a plain class library).
- The `400` body is `HttpValidationProblemDetails`, written through `IProblemDetailsService`.
  `CustomizeProblemDetails` saw it, so a custom `IProblemDetailsWriter` can produce another shape
  (such as Lighthouse's `ErrorResponse`). Keys are C# property names (`Pages`), not JSON names.
- Default messages format numbers with the server's culture
  ("between 0 and 1,7976931348623157E+308"); set `ErrorMessage`.
- `const decimal` limits (`MoneyLimits.MaxAmount`) can't be attribute arguments; `[Range]` needs
  them as strings, with `ParseLimitsInInvariantCulture = true`.
- Async validation (`AsyncValidationAttribute`, `IAsyncValidatableObject`) is .NET 11 only; for
  minimal APIs only the async path runs, and async rules run after a property's sync rules pass.

## Sources

- [Validation in ASP.NET Core (.NET 11)](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/validation?view=aspnetcore-11.0)
- [Include OpenAPI metadata in an ASP.NET Core app](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/openapi/include-metadata?view=aspnetcore-11.0)
- [What's new in ASP.NET Core in .NET 11](https://learn.microsoft.com/en-us/aspnet/core/release-notes/aspnetcore-11?view=aspnetcore-10.0)
