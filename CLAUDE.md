# CLAUDE.md

Guidance for Claude Code when working in this repository.

## What this is

One NuGet package so far, `src/Hlibz.OpenApi.AspNetCore`: transformers for
Microsoft.AspNetCore.OpenApi. The repo is named after the package family; a later
`Hlibz.OpenApi.Swashbuckle` (filters) or a dependency-free `Hlibz.OpenApi.Rules`
(`IOpenApiRule` + `RuleText`) would sit next to it. `OPENAPI_PACKAGE_FINDINGS.md` records why the
package looks the way it does (what was ported from Lighthouse, what was dropped and why).

Public API, all in namespace `Hlibz.OpenApi`, each feature an extension on `OpenApiOptions`:

- `AddOpenApiRules()`: `IOpenApiRule` attributes describe themselves (`RuleText.Add` writes
  `description` + `x-rules`); `[OpenApiRule("...")]` for text-only rules. A parameter's rules are
  also copied to the parameter's description (`Internal/OpenApiRuleParameterOperationTransformer`).
  A property whose schema will become a `$ref` gets its rule text in `x-ref-description` only, and
  its rules parked under `PendingRulesKey`.
- `AddStronglyTypedIds(valueTypeOf?)`: rewrites ID schemas to the wrapped type's schema and
  inlines them (removes `x-schema-id`, `x-is-nullable-property`), applying any `x-ref-description`
  and parked rules, so the result doesn't depend on registration order.
- `AddBearerSecurity()` / `AddSecurity(name, scheme)`: scheme on the document, requirement on
  operations where `RequiresAuthorization` (auth metadata or fallback policy, minus
  `IAllowAnonymous`). Same check as Hlibz.MinimalApi.Endpoints' internal one, copied on purpose:
  the packages don't reference each other.
- `AddValidationProblems(configure?)`: adds `400` (`HttpValidationProblemDetails`, a `$ref`) to
  operations `AddValidation()` can fail. ASP.NET Core's own test (`ValidationOptions` resolvers,
  no `IDisableValidationMetadata`, a non-service handler parameter
  `TryGetValidatableParameterInfo` accepts; ASP0029-experimental on .NET 10) **and**
  `Internal/ValidationRules` (a `ValidationAttribute`/`IValidatableObject` somewhere inside it),
  since the generator registers rule-less record structs such as IDs. Content type follows
  `IProblemDetailsService` (problem+json) or not (plain JSON: tested). A declared `400` wins.
  On .NET 10 a project may call `AddValidation()` only once (the generator fails on a second
  call site), hence `TestApp.AddValidation`.
- `AddDescriptionCleanup()`: a *document* transformer (runs last, after XML comments and `$ref`
  descriptions are in place); schemas and parameters only, never operation text.
- `services.AddOpenApiDocuments(definitions, addOpenApi, configure)`: one document per group.

`Internal/ParameterSchemas` answers "which type does this parameter schema stand for": ASP.NET
Core passes the parameter's `ParameterDescription` to *every* schema under it (an array's item
schema too), and describes a `TryParse`-bound parameter as `string`. Use it rather than reading
`ParameterDescription` directly.

- `samples/Atlas.Api`: every feature on Hlibz.MinimalApi.Endpoints (NuGet `1.0.0-preview.4`: each
  endpoint's `Map(group)` makes one `MapGet`/`MapPost` call, a group's `Configure(group)` sets its
  tags and policy, and an endpoint class's `<summary>` becomes its operation description, so write
  those for API readers and keep notes in `<remarks>`) and Hlibz.Redoc.AspNetCore,
  net11.0 only, never packed. `samples/README.md` maps each feature to where it shows; keep it,
  `Atlas.Api.http` and the sample in step with the API. Run it with
  `dotnet run --project samples/Atlas.Api` (port 5290, `/api-docs`).

The ASP.NET Core metadata keys (`x-schema-id`, `x-ref-description`, `x-is-nullable-property`) are
internal to Microsoft.AspNetCore.OpenApi and verified by the tests on both target frameworks;
recheck them when adding a target framework.

## XML comment generator facts (tested)

- It only intercepts `AddOpenApi` calls compiled in the project that has the generator: never
  calls inside this package, hence `AddOpenApiDocuments`' `addOpenApi` callback.
- `AddOpenApi(name, configure)` and lambdas are intercepted; `AddOpenApi(configureVariable)` is not.
- It's an analyzer in Microsoft.AspNetCore.OpenApi, so it only runs with a direct reference (the
  test project has one).
- Its transformers are registered before the app's own, so rules append to the XML summary.

## Requires

* The .NET SDK `global.json` pins (11.0.100, patches roll forward; Dependabot bumps it), plus
  the .NET 10 runtime to run both test targets. Pinned because warnings are errors and
  `AnalysisMode=All`: a new SDK feature band brings new analyzer rules.

## Commands

```bash
dotnet build
dotnet test
dotnet test -f net11.0
dotnet pack -c Release -o ./nupkg
dotnet run --project samples/Atlas.Api    # :5290/api-docs, try Atlas.Api.http
```

## Conventions

StyleCop + Sonar + `AnalysisMode=All`, warnings as errors; every public member has XML docs
(CS1591 on for `src`). Explicit types, no `var`. Transformers are internal, in `Internal/`; the
public surface is the `Add...` extensions, their options classes, `IOpenApiRule`, `RuleText`,
`OpenApiRuleAttribute` and `OpenApiDocumentDefinition`. Tests: xUnit v3 on Microsoft Testing
Platform, named `Subject_WithCondition_ExpectedOutcome`, asserting on the served JSON document.

Targets net10.0 and net11.0 only: Microsoft.AspNetCore.OpenApi 10 uses Microsoft.OpenApi 2.x and
11 uses 3.x (one source compiles against both; where they differ, such as
`OpenApiMediaType` vs `IOpenApiMediaType`, write code that compiles for both rather than `#if`).
.NET 9's 1.x object model is too different to support.

Versions are central (`Directory.Packages.props`); Microsoft.AspNetCore.OpenApi is a minimum per
target framework and Dependabot ignores it. Shared NuGet metadata and `Version` live in
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
