# CLAUDE.md

Guidance for Claude Code when working in this repository.

## What this is

Four NuGet packages, released together under one version: `src/Hlibz.OpenApi.AspNetCore`,
transformers for Microsoft.AspNetCore.OpenApi, `src/Hlibz.OpenApi.UseCases`, which publishes
Hlibz.UseCases, `src/Hlibz.OpenApi.Paging`, which publishes Hlibz.Paging, and
`src/Hlibz.OpenApi.Results`, which publishes Hlibz.Results' typed errors (all below). The repo is named after the package family; a later
`Hlibz.OpenApi.Swashbuckle` (filters) or a dependency-free `Hlibz.OpenApi.Rules`
(`IOpenApiRule` + `RuleText`) would sit next to them. `OPENAPI_PACKAGE_FINDINGS.md` records why the
package looks the way it does (what was ported from Lighthouse, what was dropped and why).

Public API, all in namespace `Hlibz.OpenApi`, each feature an extension on `OpenApiOptions`:

- `AddOpenApiRules()`: `IOpenApiRule` attributes describe themselves (`RuleText.Add` writes
  `description` + `x-rules`); `[OpenApiRule("...")]` for text-only rules. A parameter's rules are
  also copied to the parameter's description (`Internal/OpenApiRuleParameterOperationTransformer`).
  `AddOpenApiRules(rules => rules.Sources.Add(...))` adds rule sources (other libraries'
  self-describing attributes, as `IOpenApiRule`s); `Internal/RuleReader` is one document's
  attributes plus sources, cached. It registers its transformers once per `OpenApiOptions`
  (a `ConditionalWeakTable`): a later call only adds sources, so `AddUseCases()` and the app's
  own `AddOpenApiRules()` share one transformer whatever their order. A positional record's
  constructor parameter is read too (`JsonPropertyInfo.AssociatedParameter`), with its
  DataAnnotations (`Internal/ParameterAnnotations`), which Microsoft.AspNetCore.OpenApi reads from
  properties only (checked on both target frameworks).
  A property whose schema will become a `$ref` gets its rule text in `x-ref-description` only, and
  its rules parked under `PendingRulesKey`.
- `AddXmlDocumentation()`: `Internal/XmlDocumentation` reads `<summary>`s (and a record's
  `<param>`s, for its positional properties) from the XML file next to each assembly at runtime,
  flattened to one line; ported from Lighthouse because Microsoft.AspNetCore.OpenApi's generator
  skips other assemblies, writes crefs with their member's type and keeps
  line breaks. It *replaces* descriptions (the generator's), re-appending the rule text already
  there (`x-rules`, or the parked rules of a would-be `$ref`), so its order against
  `AddOpenApiRules()` doesn't matter; a property that will be a `$ref` gets its summary in
  `x-ref-description`. Operations are left alone, and so is the type summary on a path, query or
  header parameter's schema. Enum values' summaries (`F:` members) go in `x-enumDescriptions`
  (`Internal/EnumValueDescriptions`), every value listed since ReDoc shows it in place of `enum`,
  keyed as the schema's `enum` lists them: JSON names, numbers, or declared names (a query
  parameter binds with `Enum.TryParse`; .NET 11 inlines it with those). Package XML files reach `bin` and publish output only with
  `CopyDocumentationFilesFromPackages` (checked on the 11.0.100 SDK).
- `AddStronglyTypedIds(configure?)`: rewrites ID schemas to the wrapped type's schema and
  inlines them (removes `x-schema-id`, `x-is-nullable-property`), applying any `x-ref-description`
  and parked rules, so the result doesn't depend on registration order. With
  `AsComponents = true` a would-be `$ref` keeps its metadata and stays a component (never
  nullable itself: ASP.NET Core wraps nullable uses in `oneOf` with `null`). An array of IDs
  gets its `items` here (`DescribeItemsAsync`): ASP.NET Core writes none for a `CountryId[]`
  query parameter or for a body list of IDs with a JSON converter.
- `AddBearerSecurity()` / `AddSecurity(name, scheme)`: scheme on the document, requirement on
  operations where `RequiresAuthorization` (auth metadata or fallback policy, minus
  `IAllowAnonymous`). Same check as Hlibz.MinimalApi.Endpoints' internal one, copied on purpose:
  the packages don't reference each other.
- `AddValidationProblems(configure?)`: adds `400` (`HttpValidationProblemDetails`, a `$ref`) to
  operations `AddValidation()` can fail. ASP.NET Core's own test (`ValidationOptions` resolvers,
  no `IDisableValidationMetadata`, a non-service handler parameter
  `TryGetValidatableParameterInfo` accepts) **and**
  `Internal/ValidationRules` (a `ValidationAttribute`/`IValidatableObject` somewhere inside it),
  since the generator registers rule-less record structs such as IDs. Content type follows
  `IProblemDetailsService` (problem+json) or not (plain JSON: tested). A declared `400` wins for
  its content type: one without that content type (a typed error's `application/json`, which
  `WithResultResponses()` documents before transformers run) gets the body added beside it.
- `AddDescriptionCleanup()`: a *document* transformer (runs last, after XML comments and `$ref`
  descriptions are in place); schemas and parameters only, never operation text.
- `services.AddOpenApiDocuments(definitions, addOpenApi, configure)`: one document per group.

`Hlibz.OpenApi.UseCases` (namespace `Hlibz.OpenApi`, depends on Hlibz.UseCases, a minimum, and
on Hlibz.OpenApi.AspNetCore at the same version): `AddUseCases(configure?)` adds
`Internal/DescribedRules` (each `IDescribedRule`'s `RuleDescription` as an `IOpenApiRule`) as an
`AddOpenApiRules` source, and `Internal/UseCaseValidationOperationTransformer` a `400`
(`UseCaseOpenApiOptions`) on operations whose handler method takes a use case handler whose use
case `IUseCaseCatalog` says can fail validation (matched against `UseCaseDescriptor.HandlerType`,
no `GetInterfaces`, so it stays trimming-safe). It was a proof of concept in the Hlibz.UseCases
repository first. Its tests (`tests/Hlibz.OpenApi.UseCases.Tests`) reference Hlibz.UseCases
directly, so its generator registers their use cases. All three packages release
together, so a prerelease of Hlibz.UseCases or Hlibz.Paging makes the whole repo one (NU5104: a
stable package can't depend on a prerelease).

`Hlibz.OpenApi.Paging` (namespace `Hlibz.OpenApi`, depends on Hlibz.Paging, a minimum, and on
Hlibz.OpenApi.AspNetCore at the same version): `AddPaging()` adds `Internal/SortableByRules`
(each `[SortableBy]` as an `IOpenApiRule`: the sortable properties, format and default order as
rule text, the default order as the schema's `default`, names through the attribute's
`ConvertName`) as an `AddOpenApiRules` source. Hlibz.Paging's attribute is a plain
`ValidationAttribute` on purpose, so Paging depends on no documentation contract; this package is
where it meets OpenAPI. Its tests (`tests/Hlibz.OpenApi.Paging.Tests`) reference Hlibz.Paging
directly, so its generator sees their DTOs.

`Hlibz.OpenApi.Results` (namespace `Hlibz.OpenApi`, depends on Hlibz.Results.AspNetCore 3.x, a
minimum, and on Hlibz.OpenApi.AspNetCore at the same version): `AddResults()` adds
`Internal/DeclaredErrorsOperationTransformer`, which reads the `DeclaredErrorsMetadata` that
`WithResultResponses()`/`WithResultEnvelopes()` put on an endpoint returning
`Result<T, TError>` and `ProducesErrorSet<TErrorSet>()` on any endpoint (Hlibz.Results 3.0
preview.2): every one on the endpoint, merged. Per status code it replaces the body Hlibz.Results documented (only a `$ref`
to `ErrorResponse`/`ResultEnvelope`: a response the endpoint declares itself wins) with each
error's body component, `{ErrorSchemaId}{BodySchemaId}`: `allOf` of the body and an object
narrowing `code` (an `enum` of one) and `details` (a `$ref` to the error's own schema, only when
`ErrorDescriptor.HasDetails`), described by the error's message; several errors are a `oneOf`
with a `discriminator` on `code`. Property names come from the body's schema (the app's naming
policy). A status code with declared errors also lists the ambient errors the app's
`ResultHttpOptions.StatusCodes` sends with it (a `Result<T, TError>` accepts any of them, and
`WithResultResponses()` documents its `400`/`403` before any transformer runs, so
`AddUseCases()` sees a declared `400` and adds nothing; `AddValidationProblems()` adds its
problem only under another content type):
`ValidationFailed`'s body requires `validationErrors` and has no `details`. A status code with
only ambient errors, custom envelopes and `EnvelopeAlwaysOk` are left alone. Its tests
(`tests/Hlibz.OpenApi.Results.Tests`) reference Hlibz.Results.AspNetCore directly, so the
Hlibz.Results generator sees their errors.

`Internal/ParameterSchemas` answers "which type does this parameter schema stand for": ASP.NET
Core passes the parameter's `ParameterDescription` to *every* schema under it (an array's item
schema too), and describes a `TryParse`-bound parameter as `string`. Use it rather than reading
`ParameterDescription` directly.

- `samples/Atlas.Api`: every feature of Hlibz.OpenApi.AspNetCore and Hlibz.OpenApi.Results on
  plain minimal API endpoints (`Endpoints/`: one static class per group, handlers as `internal`
  methods whose `<summary>`/`<remarks>` Microsoft.AspNetCore.OpenApi's generator writes as the
  operation's summary/description, so write those for API readers and keep notes in `//`
  comments; `WithName` gives the `operationId`), typed errors in `Domain/Errors.cs` and
  `Domain/ErrorSets.cs`, and Hlibz.Redoc.AspNetCore, net11.0 only, never packed. No other Hlibz
  packages: Hlibz.Stack is where they all meet. `samples/README.md` maps each feature to where it shows; keep it,
  `Atlas.Api.http` and the sample in step with the API. Run it with
  `dotnet run --project samples/Atlas.Api` (port 5290, `/api-docs`). Its build writes both
  documents to `samples/Atlas.Api/openapi` (Microsoft.Extensions.ApiDescription.Server, whose
  version must match the target framework's major): committed, and CI fails when a build
  changes them, so build before committing a change that alters the documents.

The ASP.NET Core metadata keys (`x-schema-id`, `x-ref-description`, `x-is-nullable-property`) are
internal to Microsoft.AspNetCore.OpenApi and verified by the tests; recheck them when moving to a
new target framework.

## XML comment generator facts (tested)

These matter for operation descriptions from handler summaries, and for what
`AddXmlDocumentation()` replaces.

- It only intercepts `AddOpenApi` calls compiled in the project that has the generator: never
  calls inside this package, hence `AddOpenApiDocuments`' `addOpenApi` callback.
- `AddOpenApi(name, configure)` and lambdas are intercepted; `AddOpenApi(configureVariable)` is not.
- It writes a handler *method*'s `<summary>` as the operation's summary and `<remarks>` as its
  description, overriding `WithSummary`; it skips `private` methods and lambdas.
- It's an analyzer in Microsoft.AspNetCore.OpenApi, and NuGet passes it on to an app that gets
  Microsoft.AspNetCore.OpenApi only through this package (despite `exclude="Analyzers"`), but not
  the package's `build/` targets that allow its interceptors: without
  `buildTransitive/Hlibz.OpenApi.AspNetCore.targets` such an app fails with CS9137. CI's
  `.github/scripts/check-packed-package.sh` builds that app from the packed nupkg, and also runs
  it published as a single file (`Assembly.Location` is empty there, so `XmlDocumentation`
  falls back to `AppContext.BaseDirectory`; `EnableSingleFileAnalyzer` is on).
- Its transformers are registered before the app's own, so rules append to the XML summary.

## Requires

* The .NET SDK `global.json` pins (11.0.100, patches roll forward; Dependabot bumps it). Pinned because warnings are errors and
  `AnalysisMode=All`: a new SDK feature band brings new analyzer rules.

## Commands

```bash
dotnet build
dotnet test
dotnet pack -c Release -o ./nupkg
dotnet run --project samples/Atlas.Api    # :5290/api-docs, try Atlas.Api.http
```

## Conventions

StyleCop + Sonar + `AnalysisMode=All`, warnings as errors; every public member has XML docs
(CS1591 on for `src`). Explicit types, no `var`. Transformers are internal, in `Internal/`; the
public surface is the `Add...` extensions, their options classes (`OpenApiRuleOptions`,
`UseCaseOpenApiOptions`, ...), `IOpenApiRule`, `RuleText`,
`OpenApiRuleAttribute` and `OpenApiDocumentDefinition`. Tests: xUnit v3 on Microsoft Testing
Platform, named `Subject_WithCondition_ExpectedOutcome`, asserting on the served JSON document.

Targets net11.0 only, from 2.0 (Microsoft.AspNetCore.OpenApi 11, on Microsoft.OpenApi 3.x;
Hlibz.Results 3.0's C# 15 unions). 1.x targeted net10.0 and net11.0; .NET 10 apps stay there. Some
code still compiles against both object models (`With<T>` helpers); no need to keep that up.

Versions are central (`Directory.Packages.props`); Microsoft.AspNetCore.OpenApi is a minimum and
Dependabot ignores it. Shared NuGet metadata and `Version` live in
`Directory.Build.props`; release by pushing a `vX.Y.Z` tag (see Releasing). The package has its own
`src/<Package>/README.md` (packed, shown on nuget.org, absolute links only); the root `README.md`
is the full documentation; keep them in step when an API changes.

## Releasing

1. Set `Version` in `Directory.Build.props`, commit, then push the tag `vX.Y.Z`.
2. The user approves the `release` environment run.
3. Once the run succeeds and
   `https://api.nuget.org/v3-flatcontainer/<lowercase id>/index.json` lists the version, Claude
   creates the GitHub release that `PackageReleaseNotes` links to:
   `gh release create vX.Y.Z --verify-tag --title X.Y.Z [--prerelease when the version has a suffix] --notes-file <notes>`.
   Claude writes the notes from `git log <previous tag>..vX.Y.Z`: breaking changes first, then
   features and fixes, then one nuget.org link per package. Nothing is attached.
