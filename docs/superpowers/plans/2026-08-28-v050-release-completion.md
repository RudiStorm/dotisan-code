# v0.5 Contract Pipeline and Scaffolding Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Complete v0.5.0 by turning the compile-time contract manifest into deterministic OpenAPI, Zod, fetch-client, query, validation metadata, CLI generation, and editable CRUD scaffolding outputs.

**Architecture:** `Dotisan.Core` remains the canonical immutable contract model. Pure renderer libraries consume `ContractManifest` and return deterministic in-memory files/documents; the source generator remains the only endpoint/contract discovery mechanism. The CLI builds the target API, invokes an explicit generated manifest export, writes only `src/generated` and OpenAPI output, and reports stale output; the golden template wires the generated artifacts into an ordinary Vue/Vite application and keeps CRUD UI files developer-owned.

**Tech Stack:** .NET 8, ASP.NET Core Minimal APIs, Roslyn incremental source generators, System.Text.Json, xUnit, Vue 3, TypeScript, Vite, Zod, standard `fetch`, TanStack Query, npm/pnpm.

**Spec:** `docs/superpowers/specs/2026-08-27-contract-metadata-design.md`, `Dotisan Spec Kit.md`, and the v0.5 release checklist in this plan.

## Global Constraints

- Generated endpoint discovery and contract metadata MUST remain compile-time/source-generated; no runtime reflection or assembly loading.
- `ContractManifest` is the backend source of truth for all generated frontend and OpenAPI artifacts.
- Generated files live under `src/generated/` and are deterministic; scaffolded UI files are editable application source.
- Nullable and optional properties remain distinct in C#, OpenAPI, Zod, and TypeScript output.
- Generated applications remain recognizable ASP.NET Core applications and use standard DI, configuration, EF Core, FluentValidation, and `fetch`.
- No new package dependency is added to the framework unless the generated application needs the corresponding documented frontend runtime dependency.
- Every task follows RED/GREEN/REFACTOR and ends with focused verification plus a small commit.
- v0.5.0 version metadata is aligned across CLI, packages, templates, tests, and documentation.

## File Map

- Modify `src/Dotisan.Core/ContractManifest.cs` and add focused contract metadata records for transport, response, and validation information.
- Modify `src/Dotisan.SourceGenerators/EndpointRegistrationGenerator.cs` to emit the expanded manifest and a stable manifest export entry point.
- Add `src/Dotisan.OpenApi/` and `tests/Dotisan.OpenApi.Tests/` for deterministic OpenAPI 3.1 JSON rendering.
- Extend `src/Dotisan.TypeScript/` with focused renderers for Zod schemas, fetch services, query composables, validation metadata, and generated-file hashes.
- Extend `src/Dotisan.Generators/` with generated project wiring, package dependencies, generated-output directories, and editable `make:crud` files.
- Extend `src/Dotisan.Cli/` with `generate`, stale-output checking, and explicit errors for unsupported later commands.
- Modify `Dotisan.sln`, `Directory.Packages.props`, `.github/workflows/`, `README.md`, `docs/quickstart.md`, and version tests.

### Task 1: Stabilize the v0.5 contract transport model

**Files:**
- Modify: `src/Dotisan.Core/ContractManifest.cs`
- Modify: `src/Dotisan.SourceGenerators/EndpointRegistrationGenerator.cs`
- Test: `tests/Dotisan.Core.Tests/ContractManifestTests.cs`
- Test: `tests/Dotisan.SourceGenerators.Tests/SourceGeneratorTests.cs`

**Interfaces:**
- Produce `EndpointContractMetadata` with `RequestBody`, `PathParameters`, `QueryParameters`, `SuccessStatusCode`, `Tags`, and `Validation`.
- Preserve existing `ContractManifest`, `EndpointManifestEntry`, model ordering, JSON shape compatibility, and SHA-256 behavior.

- [ ] Write failing tests for deterministic transport metadata and generated manifest export.
- [ ] Run the focused Core and source-generator tests and verify they fail for the missing types/output.
- [ ] Implement immutable records and generator emission using explicit endpoint `Configure()` metadata plus route-token inference.
- [ ] Re-run focused tests, then the existing manifest and generator suites.
- [ ] Commit `feat: extend compile-time contract transport metadata`.

### Task 2: Add deterministic OpenAPI generation

**Files:**
- Create: `src/Dotisan.OpenApi/Dotisan.OpenApi.csproj`
- Create: `src/Dotisan.OpenApi/OpenApiDocumentGenerator.cs`
- Create: `tests/Dotisan.OpenApi.Tests/Dotisan.OpenApi.Tests.csproj`
- Create: `tests/Dotisan.OpenApi.Tests/OpenApiDocumentGeneratorTests.cs`
- Modify: `Dotisan.sln`

**Interfaces:**
- Produce `OpenApiDocumentGenerator.Generate(ContractManifest manifest, string title, string version)` returning a deterministic `OpenApiDocumentResult` containing JSON and SHA-256.
- Emit OpenAPI 3.1 paths, operation IDs, request bodies, path/query parameters, response schemas, nullable/optional properties, arrays, dictionaries, objects, and numeric enums.

- [ ] Add a fixture manifest and failing assertions for paths, schemas, required fields, enum values, and stable ordering.
- [ ] Run `dotnet test tests/Dotisan.OpenApi.Tests` and verify the renderer is absent/failing.
- [ ] Implement a small System.Text.Json-backed OpenAPI object model/renderer without reflection or external OpenAPI runtime dependencies.
- [ ] Verify exact JSON snapshots, hash stability, and unsupported-type errors.
- [ ] Commit `feat: add deterministic OpenAPI contract renderer`.

### Task 3: Complete TypeScript contract renderers

**Files:**
- Create: `src/Dotisan.TypeScript/ZodSchemaGenerator.cs`
- Create: `src/Dotisan.TypeScript/FetchClientGenerator.cs`
- Create: `src/Dotisan.TypeScript/TanStackQueryGenerator.cs`
- Create: `src/Dotisan.TypeScript/GeneratedFileManifest.cs`
- Test: `tests/Dotisan.AspNetCore.Tests/TypeScriptTypeMapperTests.cs`
- Create/modify: `tests/Dotisan.TypeScript.Tests/`
- Modify: `Dotisan.sln`

**Interfaces:**
- Produce deterministic `models.ts`, `schemas.ts`, `services.ts`, and `queries.ts` as `GeneratedTypeScriptFile` values.
- Generate Zod schemas with `z.object`, `z.array`, `z.record`, `z.enum`/literal numeric unions, `.nullable()`, and optional properties.
- Generate standard `fetch` services with URL interpolation, JSON request/response handling, `ProblemDetails` errors, `AbortSignal`, credentials, and correlation headers.
- Generate TanStack Query composables that call services and expose typed query/mutation options.

- [ ] Add failing renderer tests covering every supported contract type and endpoint method.
- [ ] Run the focused TypeScript test filter and verify RED.
- [ ] Implement each renderer as a pure deterministic transformation over `ContractManifest`.
- [ ] Add golden output assertions, hash assertions, and explicit `Unknown` type errors.
- [ ] Commit `feat: generate zod clients and query composables`.

### Task 4: Emit validation metadata without replacing FluentValidation

**Files:**
- Modify: `src/Dotisan.Core/ContractManifest.cs`
- Modify: `src/Dotisan.SourceGenerators/EndpointRegistrationGenerator.cs`
- Create: `src/Dotisan.TypeScript/ValidationSchemaGenerator.cs`
- Test: `tests/Dotisan.Core.Tests/ContractManifestTests.cs`
- Test: `tests/Dotisan.SourceGenerators.Tests/SourceGeneratorTests.cs`
- Test: `tests/Dotisan.TypeScript.Tests/ValidationSchemaGeneratorTests.cs`

**Interfaces:**
- Emit portable validation metadata for required, length, range, email, and pattern rules when declared through the endpoint metadata contract.
- Keep runtime FluentValidation authoritative; generated Zod output must document that it is client-side guidance and reject unknown rule kinds with a clear diagnostic.

- [ ] Add failing metadata and output tests, including nullable versus required behavior.
- [ ] Run the focused tests and confirm RED.
- [ ] Implement explicit metadata collection and Zod rule rendering.
- [ ] Verify unsupported rules produce actionable source-generator/CLI messages.
- [ ] Commit `feat: emit portable validation contract metadata`.

### Task 5: Add explicit generated manifest export and CLI generation

**Files:**
- Modify: `src/Dotisan.SourceGenerators/EndpointRegistrationGenerator.cs`
- Modify: `src/Dotisan.Cli/BuiltInCommands.cs`
- Modify: `src/Dotisan.Cli/DotisanServices.cs`
- Create: `src/Dotisan.Cli/Generation/ContractGenerationService.cs`
- Test: `tests/Dotisan.Cli.Tests/CliApplicationTests.cs`
- Test: `tests/Dotisan.Cli.Tests/GenerationCommandTests.cs`

**Interfaces:**
- Add `dotisan generate [--check] [--no-openapi]`.
- The command locates the generated API project, builds it, runs an explicit generated export target, writes deterministic files under `src/generated`, writes `openapi.json`, and exits non-zero when `--check` detects stale output.
- Errors identify the missing API project, failed build, unavailable package manager, invalid manifest, or stale file path and include the next command to run.

- [ ] Add failing CLI tests for generation, check mode, missing workspace, and stable errors.
- [ ] Run the focused CLI tests and verify RED.
- [ ] Implement the service using existing process/console abstractions; never load the API assembly in the CLI.
- [ ] Verify generated file contents, SHA-256 marker, check mode, and cancellation behavior.
- [ ] Commit `feat: add contract generation command`.

### Task 6: Wire the golden Vue/Vite template and npm/pnpm dependencies

**Files:**
- Modify: `src/Dotisan.Generators/GoldenTemplateGenerator.cs`
- Modify: `src/Dotisan.Generators/TemplateFiles.cs`
- Modify: `tests/Dotisan.Generators.Tests/GoldenTemplateGeneratorTests.cs`
- Modify: `README.md`
- Modify: `docs/quickstart.md`

**Interfaces:**
- Generated Vue projects contain `src/generated/models.ts`, `schemas.ts`, `services.ts`, and `queries.ts` placeholders/outputs, standard package scripts, `zod`, `@tanstack/vue-query`, and the selected npm/pnpm lockfile workflow.
- `dotisan new` continues to run the selected package manager install and reports exact retry instructions on failure.
- Generated API exposes the explicit manifest export used by `dotisan generate` and remains buildable before any endpoint is added.

- [ ] Add failing generated-template assertions for package dependencies, scripts, generated directories, and the first `dotisan new`/`dotisan generate` flow.
- [ ] Run the focused generator tests and verify RED.
- [ ] Implement template wiring and generated frontend bootstrap/query provider setup using ordinary Vue code.
- [ ] Verify generated API build, generated file compilation, package install, and Vite build when network permits.
- [ ] Commit `feat: wire generated contract pipeline into golden template`.

### Task 7: Add editable CRUD/UI scaffolding

**Files:**
- Modify: `src/Dotisan.Generators/GoldenTemplateGenerator.cs`
- Modify: `src/Dotisan.Generators/TemplateFiles.cs`
- Modify: `src/Dotisan.Cli/BuiltInCommands.cs`
- Modify: `src/Dotisan.Cli/DotisanServices.cs`
- Create: `tests/Dotisan.Cli.Tests/CrudScaffoldingTests.cs`
- Modify: `tests/Dotisan.Generators.Tests/GoldenTemplateGeneratorTests.cs`

**Interfaces:**
- Add `dotisan make:crud <ResourceName>` that writes editable list/detail/form/page/route/navigation files and never places them under `src/generated`.
- Reuse the generated service/query names and reject invalid resource names with a usage error.

- [ ] Add failing tests for file list, idempotent collision errors, and generated-owned versus scaffolded paths.
- [ ] Run the focused tests and verify RED.
- [ ] Implement minimal vertical Vue CRUD pages with loading, empty, error, create, edit, and delete states.
- [ ] Verify the generated frontend builds after scaffolding and commit `feat: scaffold editable frontend crud`.

### Task 8: Release closeout, CI, and documentation

**Files:**
- Modify: `src/Dotisan.Cli/DotisanApplication.cs`
- Modify: `src/Dotisan.Cli/Dotisan.Cli.csproj`
- Modify: `src/Dotisan.Generators/TemplateFiles.cs`
- Modify: version assertions in `tests/`
- Modify: `README.md`, `docs/quickstart.md`, `docs/superpowers/specs/2026-08-27-contract-metadata-design.md`
- Modify/create: `.github/workflows/ci.yml`

- [ ] Add failing version and CI documentation assertions.
- [ ] Align all version values to `0.5.0`, package scripts, generated output instructions, stale checks, and `make:crud` examples.
- [ ] Add CI restore/build/test, generated API smoke test, and conditional frontend install/build with exact network blocker reporting.
- [ ] Run `dotnet restore`, `dotnet build Dotisan.sln --configuration Release`, `dotnet test Dotisan.sln --configuration Release`, `dotnet pack`, `git diff --check`, and a generated-project smoke test.
- [ ] Record exact failures and limitations; commit `chore: close out v0.5.0`.

## Self-Review

- OpenAPI, Zod, fetch, TanStack Query, template wiring, CLI generation, validation metadata, CRUD scaffolding, versions, docs, CI, and smoke tests each have a task.
- All later interfaces are named in earlier task contracts or introduced in the same task.
- No runtime reflection, custom DI/ORM/configuration replacement, or opaque plugin runtime is introduced.
- Generated code and scaffolded code have separate ownership and output paths.
- Any dependency restore/build limitation must be reported with the exact command and error text rather than summarized as a generic network issue.
