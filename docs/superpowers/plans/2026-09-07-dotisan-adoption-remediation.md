# Dotisan Adoption Remediation Implementation Plan

## Execution status (2026-09-08)

The acceptance checklist below is verified on `master`. Task 7's generated-project source migration is complete: selected API, integration, frontend, and test outputs now render from normalized physical embedded templates, while `TemplateFiles.cs` remains only as the orchestration/scaffolding implementation. Interaction-level frontend coverage (Task 12, Step 4) is verified for login errors, antiforgery propagation, session revocation, notifications, imports/exports, and webhooks. The release matrix has been exercised with minimal/npm, identity/pnpm, saas/npm, and maximal/pnpm profiles. A packed-tool clean-machine smoke test and `.nupkg`/`.snupkg` inspection also pass, including locally prepared `0.9.0-beta.1` artifacts. Publishing a prerelease package remains an external release action and has not been performed.

Subsequent hardening on `master` also keeps generated `Program.cs` below the host-composition boundary, enforces bounded import streaming, adds bounded notification pagination, derives the generated frontend version from the shared MSBuild assembly version, and runs the complete release matrix through `scripts/Test-Release.ps1`. The latest release-gate run passed all repository, generated-profile, frontend, audit, and package-inspection checks.

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make Dotisan's documented project-generation workflow reliable, idiomatic for .NET developers, secure by default, reproducible, and credible as a public developer tool.

**Architecture:** Use the ASP.NET Core OpenAPI document as the canonical API contract and treat `dotisan.contract.json`, TypeScript clients, Zod schemas, TanStack Query helpers, and checked-in OpenAPI as derived artifacts. Keep the CLI as a thin orchestration tool, move generated source into physical template files, and make jobs and integrations opt-in capabilities with explicit production-readiness labels.

**Tech Stack:** .NET 10, ASP.NET Core Minimal APIs, Microsoft.AspNetCore.OpenApi, Microsoft.Extensions.ApiDescription.Server, EF Core 10, xUnit, Vue 3, TypeScript, Vite, Vitest, Playwright, npm/pnpm, GitHub Actions, NuGet.

**Spec:** `README.md`, `docs/quickstart.md`, `docs/v086-acceptance.md`, and the architecture assessment accepted in the 2026-09-07 Codex task.

## Global Constraints

- Preserve the principle that generated application code belongs to the application developer.
- Generated applications must remain runnable with standard `dotnet`, EF Core, npm, and pnpm commands after Dotisan is removed.
- Do not require a proprietary Dotisan runtime to map or execute application endpoints.
- Target stable .NET 10 and ASP.NET Core 10; do not introduce preview APIs.
- Use standard ASP.NET Core OpenAPI metadata as the contract source of truth.
- Never overwrite a valid generated artifact until every replacement artifact has been produced and validated.
- Advanced capabilities must be opt-in and must not appear production-ready while containing stubs or process-local state.
- Every task uses test-first development and ends with a focused commit.
- Do not mix unrelated formatting or refactoring into a task.
- Do not modify or discard unrelated existing worktree changes.

---

## Delivery order and release policy

| Phase | Tasks | Release meaning |
| --- | --- | --- |
| 0: Stabilize | 1-5 | Required before publishing another package |
| 1: Normalize | 6-9 | Required before calling the project beta-ready |
| 2: Harden | 10-13 | Required before recommending generated apps for production foundations |
| 3: Earn trust | 14-16 | Required for a credible public 1.0 release |

Do not begin a later phase while an earlier phase has a failing acceptance gate.

## Planned file structure

### Contract pipeline

- `src/Dotisan.Core/ContractManifest.cs`: deterministic derived contract model and correct JSON round trips.
- `src/Dotisan.Cli/Generation/OpenApiContractReader.cs`: convert an ASP.NET Core OpenAPI 3.1 document into Dotisan's internal contract model.
- `src/Dotisan.Cli/Generation/ContractGenerationService.cs`: orchestrate build-time OpenAPI export, validation, rendering, and atomic publication.
- `src/Dotisan.Cli/Generation/GeneratedArtifact.cs`: immutable relative path/content/checksum value.
- `src/Dotisan.Cli/Generation/AtomicArtifactPublisher.cs`: stage and atomically publish generated files.
- `src/Dotisan.TypeScript/*Generator.cs`: pure renderers that consume the derived contract.
- `src/Dotisan.Generators/Templates/`: physical source templates copied into generated projects.

### Generated application

- `src/<Name>.Api/Program.cs`: host composition only.
- `src/<Name>.Api/Infrastructure/ServiceCollectionExtensions.cs`: core service registration.
- `src/<Name>.Api/Infrastructure/ApplicationBuilderExtensions.cs`: middleware ordering.
- `src/<Name>.Api/Features/<Feature>/`: route groups, DTOs, validation, and feature behavior.
- `src/<Name>.Web/src/api/`: generated API artifacts; no handwritten UI files.
- `src/<Name>.Web/src/features/`: editable application UI and feature code.

### Validation

- `tests/Dotisan.Core.Tests/ContractManifestTests.cs`: serialization invariants.
- `tests/Dotisan.Cli.Tests/GenerationCommandTests.cs`: atomic generation and failure behavior.
- `tests/Dotisan.Cli.Tests/GoldenPathTests.cs`: consumer workflows.
- `tests/Dotisan.TypeScript.Tests/FetchClientGeneratorTests.cs`: path/query/body signatures.
- `.github/workflows/ci.yml`: repository, generated-project, package, and release gates.

---

## Phase 0: Stabilize the advertised workflow

### Task 1: Add a failing end-to-end reproduction gate

**Files:**
- Create: `tests/Dotisan.Cli.Tests/GoldenPathTests.cs`
- Modify: `tests/Dotisan.Cli.Tests/Dotisan.Cli.Tests.csproj`
- Reuse: `src/Dotisan.Testing/TemporaryDirectory.cs`

**Interfaces:**
- Consumes: `DotisanApplication.CreateDefault(...)` and generated workspace commands.
- Produces: a regression test proving `new -> make:resource -> generate -> generate --check -> build`.

- [x] **Step 1: Write an end-to-end test that generates an authenticated application, scaffolds `Customer`, generates contracts, and verifies output**

```csharp
[Fact]
public async Task Resource_generation_updates_contract_and_preserves_a_buildable_frontend()
{
    using var directory = new TemporaryDirectory();
    var root = Path.Combine(directory.Path, "GoldenApp");

    Assert.Equal(DotisanExitCode.Success, await RunFromRepositoryAsync(
        "new", "GoldenApp", "--yes", "--auth", "yes",
        "--registration", "public", "--no-restore", "--output", root));
    Assert.Equal(DotisanExitCode.Success, await RunFromAsync(root, "make:resource", "Customer"));
    Assert.Equal(DotisanExitCode.Success, await RunFromAsync(root, "generate"));
    Assert.Equal(DotisanExitCode.Success, await RunFromAsync(root, "generate", "--check"));

    var services = await File.ReadAllTextAsync(
        Path.Combine(root, "src", "GoldenApp.Web", "src", "api", "services.ts"));
    Assert.Contains("listCustomers", services, StringComparison.Ordinal);
    Assert.Contains("customerId", services, StringComparison.Ordinal);
}
```

- [x] **Step 2: Run only the new test and confirm the current failure**

```powershell
dotnet test tests\Dotisan.Cli.Tests\Dotisan.Cli.Tests.csproj -c Release --filter FullyQualifiedName~GoldenPathTests
```

Expected: FAIL because `dotisan generate` throws or the Customer operations are absent.

- [x] **Step 3: Add assertions that a failed generation leaves every existing generated file byte-for-byte unchanged**

Capture SHA-256 hashes for `models.ts`, `schemas.ts`, `services.ts`, `queries.ts`, `openapi.json`, and `dotisan.contract.json`; force an invalid contract; assert all hashes remain unchanged.

- [x] **Step 4: Commit the red tests**

```powershell
git add tests/Dotisan.Cli.Tests
git commit -m "test: reproduce broken contract generation workflow"
```

### Task 2: Correct `ContractManifest` JSON round trips

**Files:**
- Modify: `src/Dotisan.Core/ContractManifest.cs:10`
- Modify: `tests/Dotisan.Core.Tests/ContractManifestTests.cs`

**Interfaces:**
- Consumes: JSON with `schemaVersion`, `endpoints`, `endpointMetadata`, and `models`.
- Produces: `ContractManifest.FromJson(string)` that preserves aligned endpoint metadata.

- [x] **Step 1: Add a failing metadata round-trip test**

```csharp
[Fact]
public void Json_round_trip_preserves_endpoint_metadata()
{
    var original = ContractFixtures.ManifestWithPathQueryBodyAndValidation();
    var restored = ContractManifest.FromJson(original.ToJson());

    Assert.NotNull(restored.EndpointMetadata);
    Assert.Equal(original.EndpointMetadata, restored.EndpointMetadata);
    Assert.Equal(original.Sha256, restored.Sha256);
}
```

- [x] **Step 2: Run the focused test and confirm metadata is currently null**

```powershell
dotnet test tests\Dotisan.Core.Tests\Dotisan.Core.Tests.csproj -c Release --filter Json_round_trip_preserves_endpoint_metadata
```

- [x] **Step 3: Replace the three-argument JSON constructor with the complete constructor**

```csharp
[JsonConstructor]
public ContractManifest(
    int SchemaVersion,
    IReadOnlyList<EndpointManifestEntry> Endpoints,
    IReadOnlyList<ContractModel> Models,
    IReadOnlyList<EndpointContractMetadata>? EndpointMetadata = null)
{
    // Retain validation and deterministic endpoint/metadata ordering.
}
```

Remove `[JsonConstructor]` from any overload that cannot reconstruct the complete serialized shape.

- [x] **Step 4: Add tests for missing metadata, count mismatch, deterministic ordering, and legacy empty manifests**

- [x] **Step 5: Run the core and OpenAPI suites**

```powershell
dotnet test tests\Dotisan.Core.Tests\Dotisan.Core.Tests.csproj -c Release
dotnet test tests\Dotisan.OpenApi.Tests\Dotisan.OpenApi.Tests.csproj -c Release
```

Expected: all tests pass with zero failed tests.

- [x] **Step 6: Commit**

```powershell
git add src/Dotisan.Core/ContractManifest.cs tests/Dotisan.Core.Tests
git commit -m "fix: preserve endpoint metadata during contract deserialization"
```

### Task 3: Make contract publication atomic and CLI failures controlled

**Files:**
- Create: `src/Dotisan.Cli/Generation/GeneratedArtifact.cs`
- Create: `src/Dotisan.Cli/Generation/AtomicArtifactPublisher.cs`
- Modify: `src/Dotisan.Cli/Generation/ContractGenerationService.cs`
- Modify: `src/Dotisan.Cli/BuiltInCommands.cs:196`
- Modify: `tests/Dotisan.Cli.Tests/GenerationCommandTests.cs`

**Interfaces:**
- Produces: `GeneratedArtifact(string RelativePath, string Content)`.
- Produces: `AtomicArtifactPublisher.PublishAsync(string root, IReadOnlyList<GeneratedArtifact> artifacts, CancellationToken)`.
- Guarantees: no target file changes if rendering or validation fails.

- [x] **Step 1: Write failing publisher tests for success, renderer failure, cancellation, and replacement of existing files**

```csharp
public sealed record GeneratedArtifact(string RelativePath, string Content);

public interface IGeneratedArtifactPublisher
{
    Task PublishAsync(
        string root,
        IReadOnlyList<GeneratedArtifact> artifacts,
        CancellationToken cancellationToken);
}
```

- [x] **Step 2: Implement staging beneath `.dotisan/staging/<guid>`**

Validate that every normalized artifact path remains beneath the target root. Write and validate all staged files first. Replace target files only after staging completes. Always remove the staging directory in `finally`.

- [x] **Step 3: Change `ContractGenerationService` to compute every artifact before publishing**

The order must be: acquire canonical contract, parse, validate, render all TypeScript, render OpenAPI/derived manifest, compare for `--check`, then publish once.

- [x] **Step 4: Return a typed failure instead of allowing renderer exceptions to reach `Program.cs`**

```csharp
catch (Exception exception) when (exception is
    JsonException or IOException or InvalidOperationException or ArgumentException)
{
    return ContractGenerationResult.Failed($"Contract generation failed: {exception.Message}");
}
```

Log stack traces only behind an explicit `--verbose` option.

- [x] **Step 5: Run the focused CLI tests and the red golden-path test**

```powershell
dotnet test tests\Dotisan.Cli.Tests\Dotisan.Cli.Tests.csproj -c Release --filter "GenerationCommandTests|GoldenPathTests"
```

- [x] **Step 6: Commit**

```powershell
git add src/Dotisan.Cli/Generation src/Dotisan.Cli/BuiltInCommands.cs tests/Dotisan.Cli.Tests
git commit -m "fix: publish generated contracts atomically"
```

### Task 4: Make ASP.NET Core OpenAPI the canonical contract

**Files:**
- Create: `src/Dotisan.Cli/Generation/OpenApiContractReader.cs`
- Create: `tests/Dotisan.Cli.Tests/OpenApiContractReaderTests.cs`
- Modify: `src/Dotisan.Cli/Generation/ContractGenerationService.cs`
- Modify template: `src/Dotisan.Generators/Templates/Api/Api.csproj`
- Modify templates: endpoint files under `src/Dotisan.Generators/Templates/Api/Features/`
- Remove from generated template: `Infrastructure/DotisanContractExport.cs`
- Deprecate: `src/Dotisan.SourceGenerators/EndpointRegistrationGenerator.cs`

**Interfaces:**
- Consumes: a build-produced OpenAPI 3.1 JSON document.
- Produces: `ContractManifest OpenApiContractReader.Read(string json)`.
- Defines: `dotisan.contract.json` is derived output and never hand-edited input.

- [x] **Step 1: Add contract-reader fixtures covering route parameters, query parameters, request bodies, nullable fields, arrays, enums, errors, authorization, and validation constraints**

- [x] **Step 2: Configure generated API projects for build-time OpenAPI output**

```xml
<PackageReference Include="Microsoft.Extensions.ApiDescription.Server" PrivateAssets="all" />
<PropertyGroup>
  <OpenApiGenerateDocuments>true</OpenApiGenerateDocuments>
</PropertyGroup>
```

Write build output beneath `obj/dotisan/openapi`; do not write generated artifacts into source directories during an ordinary build.

- [x] **Step 3: Add complete ASP.NET Core metadata to every generated endpoint**

Use stable endpoint names, tags, `TypedResults`, `.Accepts<T>()`, `.Produces<T>()`, `.ProducesProblem(...)`, authorization metadata, and built-in .NET 10 validation. Remove inferred success codes that assume every POST returns 201.

- [x] **Step 4: Implement `OpenApiContractReader.Read` as a pure conversion**

Reject duplicate operation IDs, missing operation IDs, unresolved schema references, unsupported request shapes, or path-template/parameter mismatches with an error containing the operation ID and source path.

- [x] **Step 5: Update `dotisan generate` to build the API document and derive all downstream artifacts from it**

The command must not start the application, connect to the database, run migrations, or instantiate production integrations.

- [x] **Step 6: Remove `DOTISAN_CONTRACT_FALLBACK` and the embedded manifest constant from generated projects**

- [x] **Step 7: Mark the old source-generator path obsolete for one release and document removal timing**

If no shipped consumer can depend on it yet, remove the unused projects from `Dotisan.sln` instead of preserving dead architecture.

- [x] **Step 8: Run contract, OpenAPI, generator, CLI, and golden-path tests**

```powershell
dotnet test Dotisan.sln -c Release --no-restore
```

- [x] **Step 9: Commit**

```powershell
git add src tests
git commit -m "feat: derive client contracts from ASP.NET Core OpenAPI"
```

### Task 5: Correct TypeScript path, query, body, and response generation

**Files:**
- Modify: `src/Dotisan.TypeScript/FetchClientGenerator.cs`
- Modify: `src/Dotisan.TypeScript/TanStackQueryGenerator.cs`
- Create: `tests/Dotisan.TypeScript.Tests/FetchClientGeneratorTests.cs`
- Create: `tests/Dotisan.TypeScript.Tests/TanStackQueryGeneratorTests.cs`
- Add project: `tests/Dotisan.TypeScript.Tests/Dotisan.TypeScript.Tests.csproj`
- Modify: `Dotisan.sln`

**Interfaces:**
- Produces path parameters as required function arguments.
- Produces optional query parameters through `URLSearchParams`.
- Produces body arguments only for operations with request bodies.
- Encodes every path and query value.

- [x] **Step 1: Add failing tests for `/users/{id:guid}`, optional queries, multiple parameters, reserved characters, void responses, and ProblemDetails**

Expected generated shape:

```typescript
export async function getUser(
  id: string,
  query: { includeHistory?: boolean } = {},
  options: RequestInit = {},
): Promise<UserResponse> {
  const search = new URLSearchParams();
  if (query.includeHistory !== undefined) {
    search.set("includeHistory", String(query.includeHistory));
  }
  return request<UserResponse>(
    `/api/users/${encodeURIComponent(id)}${search.size ? `?${search}` : ""}`,
    { method: "GET", ...options },
  );
}
```

- [x] **Step 2: Replace string concatenation in `BuildUrl` with structured path/query rendering**

- [x] **Step 3: Ensure TanStack Query keys include every path and query argument**

- [x] **Step 4: Run TypeScript generator tests and compile a generated authenticated frontend**

```powershell
dotnet test tests\Dotisan.TypeScript.Tests\Dotisan.TypeScript.Tests.csproj -c Release
dotisan new ContractWeb --yes --auth yes --registration public --output $env:TEMP\dotisan-contract-web
npm run build --prefix $env:TEMP\dotisan-contract-web\src\ContractWeb.Web
```

- [x] **Step 5: Commit**

```powershell
git add src/Dotisan.TypeScript tests/Dotisan.TypeScript.Tests Dotisan.sln
git commit -m "fix: generate valid typed path and query clients"
```

---

## Phase 1: Normalize the developer experience

### Task 6: Add CI coverage for real consumer journeys

**Files:**
- Modify: `.github/workflows/ci.yml`
- Create: `scripts/Test-GeneratedProject.ps1`
- Create: `scripts/test-generated-project.sh`

**Interfaces:**
- Consumes: profile, package manager, authentication flag, and database provider.
- Produces: one repeatable local/CI generated-project acceptance command.

- [x] **Step 1: Extract generated-project verification into scripts that stop on the first non-zero exit code**

```powershell
$ErrorActionPreference = 'Stop'
dotnet restore $solution
dotnet build $solution -c Release --no-restore --warnaserror
dotnet test $solution -c Release --no-build --no-restore
& $packageManager install --prefix $web
& $packageManager run build --prefix $web
& $packageManager test --prefix $web
```

- [x] **Step 2: Add a CI matrix for `minimal`, `identity`, `saas`, and `maximal` with npm and pnpm**

- [x] **Step 3: Run `make:resource`, `make:crud`, `generate`, and `generate --check` in identity and maximal jobs**

- [x] **Step 4: Add a generated frontend audit job**

Run both `npm audit --omit=dev --audit-level=high` and a full-tree audit whose accepted exceptions are documented with expiry dates.

- [x] **Step 5: Add at least one SQL Server or PostgreSQL Compose smoke test; keep SQLite on every matrix row**

- [x] **Step 6: Run both scripts locally against one profile, then commit**

```powershell
git add .github/workflows/ci.yml scripts
git commit -m "ci: verify complete generated application journeys"
```

### Task 7: Move generated source out of `TemplateFiles.cs`

**Files:**
- Create: `src/Dotisan.Generators/Templates/**`
- Create: `src/Dotisan.Generators/TemplateCatalog.cs`
- Create: `src/Dotisan.Generators/TemplateRenderer.cs`
- Modify: `src/Dotisan.Generators/Dotisan.Generators.csproj`
- Delete after migration: `src/Dotisan.Generators/TemplateFiles.cs`
- Modify: `tests/Dotisan.Generators.Tests/GoldenTemplateGeneratorTests.cs`

**Interfaces:**
- Produces: `TemplateCatalog.Select(ProjectOptions)` returning physical template resources.
- Produces: `TemplateRenderer.Render(TemplateResource, TemplateContext)`.

- [x] **Step 1: Add a test comparing a rendered minimal fixture with approved physical files**

- [x] **Step 2: Create one template file per generated file, preserving normal editor syntax support**

Use explicit tokens such as `__PROJECT_NAME__` and conditional file selection. Do not place C# control flow inside `.cs`, `.vue`, JSON, YAML, or Dockerfile templates.

- [x] **Step 3: Embed the template directory as resources and implement normalized-path rendering**

- [x] **Step 4: Run `dotnet format` or the relevant formatter on generated code during template development, not during end-user generation**

- [x] **Step 5: Add golden tests for minimal, identity, saas, maximal, each database provider, and each package manager**

- [x] **Step 6: Remove generated source bodies from `TemplateFiles.cs` after every selected output was covered by golden verification**

- [x] **Step 7: Commit**

```powershell
git add src/Dotisan.Generators tests/Dotisan.Generators.Tests
git commit -m "refactor: move generated applications to physical templates"
```

### Task 8: Introduce clear profiles and remove heavyweight defaults

**Files:**
- Modify: `src/Dotisan.Core/ProjectOptions.cs`
- Modify: `src/Dotisan.Cli/BuiltInCommands.cs`
- Modify: `src/Dotisan.Cli/ConsoleAndPrompts.cs`
- Modify: templates under `src/Dotisan.Generators/Templates/`
- Modify: `tests/Dotisan.Core.Tests/ProjectOptionsTests.cs`
- Modify: `tests/Dotisan.Cli.Tests/PromptChoiceTests.cs`

**Interfaces:**
- Add: `ProjectProfile { Minimal, Identity, Saas, Custom }`.
- Add: `JobProvider { None, Wolverine }`.
- Default: minimal profile, SQLite, no authentication, no Wolverine, basic ASP.NET Core logging, no OTLP exporter packages.

- [x] **Step 1: Write profile-expansion tests with exact expected options and package references**

- [x] **Step 2: Add `--profile minimal|identity|saas|custom` and `--jobs none|wolverine`**

- [x] **Step 3: Keep existing flags as explicit custom-profile overrides and reject contradictory combinations**

- [x] **Step 4: Make the review screen show every dependency-bearing choice before file creation**

- [x] **Step 5: Verify the minimal API project has no Wolverine, Identity, SignalR, or OpenTelemetry exporter references**

- [x] **Step 6: Commit**

```powershell
git add src tests
git commit -m "feat: add focused project profiles and lightweight defaults"
```

### Task 9: Make CLI conventions predictable for .NET developers

**Files:**
- Split: `src/Dotisan.Cli/BuiltInCommands.cs`
- Create: `src/Dotisan.Cli/Commands/<CommandName>Command.cs`
- Modify: `src/Dotisan.Cli/CommandRegistry.cs`
- Modify: `tests/Dotisan.Cli.Tests/CliApplicationTests.cs`
- Modify: `README.md`

**Interfaces:**
- Canonical forms: `dotisan make resource`, `dotisan make endpoint`, `dotisan make crud`, `dotisan add integration`, `dotisan remove integration`.
- Compatibility aliases: retain colon commands for one documented deprecation cycle.
- Every command supports `--help`; root supports `--version` and machine-readable `--json` where useful.

- [x] **Step 1: Add parser tests for nested commands, aliases, help, unknown options, missing values, cancellation, and exit codes**

- [x] **Step 2: Move each command into a focused file without changing behavior**

- [x] **Step 3: Add canonical space-separated commands and deprecation warnings for aliases**

- [x] **Step 4: Ensure command failures never print an unhandled stack trace by default**

- [x] **Step 5: Generate shell completion documentation or scripts for PowerShell, bash, and zsh**

- [x] **Step 6: Commit**

```powershell
git add src/Dotisan.Cli tests/Dotisan.Cli.Tests README.md
git commit -m "refactor: align CLI commands with dotnet conventions"
```

---

## Phase 2: Harden generated applications

### Task 10: Refactor generated host composition

**Files:**
- Modify template: `Templates/Api/Program.cs`
- Create templates: `Templates/Api/Infrastructure/ServiceCollectionExtensions.cs`
- Create templates: `Templates/Api/Infrastructure/ApplicationBuilderExtensions.cs`
- Create templates: `Templates/Api/Infrastructure/FeatureRegistrationExtensions.cs`
- Modify generated API tests.

**Interfaces:**
- Produces: `AddApplicationServices(builder.Configuration, builder.Environment)`.
- Produces: `UseApplicationPipeline()`.
- Produces: `MapApplicationEndpoints()`.

- [x] **Step 1: Add generated-project assertions that `Program.cs` remains below 80 lines and contains no feature-specific registrations**

- [x] **Step 2: Extract configuration, OpenTelemetry, EF Core, Identity, jobs, and optional features into focused registration methods**

- [x] **Step 3: Remove duplicate `AddHttpClient()` calls and preserve deliberate middleware ordering**

- [x] **Step 4: Add HSTS outside Development and keep forwarded headers before redirects, authentication, and link generation**

- [x] **Step 5: Run generated integration tests behind forwarded HTTPS headers**

- [x] **Step 6: Commit**

```powershell
git add src/Dotisan.Generators/Templates tests
git commit -m "refactor: simplify generated application composition"
```

### Task 11: Apply authorization, antiforgery, and validation consistently

**Files:**
- Modify templates for Account, Authorization, Jobs, Notifications, Storage, Imports/Exports, and Webhooks endpoints.
- Create template: `Infrastructure/EndpointConventionExtensions.cs`
- Modify generated API tests for every state-changing route.

**Interfaces:**
- Produces: `RouteGroupBuilder RequireCookieMutationProtection()`.
- Uses: .NET 10 `AddValidation()` for request DTO validation.
- Requires: explicit permission policies for administrative and cross-user operations.

- [x] **Step 1: Add integration tests proving every cookie-authenticated POST, PUT, PATCH, and DELETE rejects a missing or invalid antiforgery token**

- [x] **Step 2: Protect sample job endpoints or omit them from authenticated production profiles**

- [x] **Step 3: Require feature permissions for role assignment, sending notifications to other users, storage deletion, import/export, and webhook replay**

- [x] **Step 4: Replace scattered manual string checks with DTO validation and standard ValidationProblem responses**

- [x] **Step 5: Add tests for 401, 403, 400, valid token success, and tenant isolation**

- [x] **Step 6: Commit**

```powershell
git add src/Dotisan.Generators/Templates tests
git commit -m "fix: secure generated mutation endpoints by default"
```

### Task 12: Graduate or demote every optional integration

**Files:**
- Modify templates under `Templates/Api/Integrations/`
- Modify matching Vue feature templates.
- Create: `docs/integration-readiness.md`
- Modify: `README.md`

**Interfaces:**
- Readiness labels: `production-foundation`, `development-adapter`, `example-only`.
- CLI output must print the label and required follow-up work for each selected integration.

- [x] **Step 1: Create the readiness matrix with explicit behavior**

```markdown
| Capability | Required behavior before production-foundation |
| --- | --- |
| Imports | bounded upload, durable status, real handler, tenant ownership |
| Storage | streaming limits, ownership policy, preserved content type, durable provider |
| Webhooks | background dispatch, timeout, fresh replay signature, allowlist, observable failures |
| Notifications | authenticated user mapping, authorization, persistence, pagination |
| Caching | distributed implementation guidance, namespaced keys, invalidation example |
```

- [x] **Step 2: Imports: add upload limits, durable status persistence, an `ImportRequested` handler, format validation, and tenant ownership**

- [x] **Step 3: Storage: remove the throwing S3 class unless a working adapter is selected; stream without relying on `Stream.Length`; persist content type and owner**

- [x] **Step 4: Webhooks: dispatch through Wolverine, configure `HttpClient.Timeout`, validate configured destinations, record terminal error details safely, and recompute signatures during replay**

- [x] **Step 5: Notifications: add `IUserIdProvider`, pagination, permission checks, SignalR authorization, and delivery tests**

- [x] **Step 6: Mark any capability failing its row as `example-only` and remove production-readiness language from CLI and README**

- [x] **Step 7: Commit each capability separately after its tests pass**

```powershell
git commit -m "feat: harden generated import foundation"
git commit -m "feat: harden generated storage foundation"
git commit -m "feat: dispatch generated webhooks durably"
git commit -m "feat: harden generated notifications"
```

### Task 13: Improve generated frontend behavior and coverage

**Files:**
- Modify templates under `Templates/Web/src/`
- Create template: `Templates/Web/src/api/client.ts`
- Create feature tests adjacent to each generated page.
- Modify: `Templates/Web/tests/e2e/shell.spec.ts`

**Interfaces:**
- All API requests use the generated client boundary.
- Mutation helpers acquire/send antiforgery tokens automatically when cookie authentication is selected.
- Pages expose loading, empty, success, and error states.

- [x] **Step 1: Replace direct page-level `fetch` calls with generated services or a single typed client**

- [x] **Step 2: Add pending-state disabling, error messages, success confirmation, and retry behavior to imports, notifications, webhooks, sessions, and authorization pages**

- [x] **Step 3: Add accessible labels, table captions, focus styles, status announcements, and confirmation for destructive session actions**

- [x] **Step 4: Add Vitest tests for login errors, antiforgery propagation, session revocation, imports, notifications, and webhook replay**

- [x] **Step 5: Expand Playwright from a shell-heading check to registration, login, navigation, failed request, and protected-route flows**

- [x] **Step 6: Add `PRODUCT.md` and `DESIGN.md` for Dotisan's own generated portal baseline, while clearly documenting that applications should customize them**

- [x] **Step 7: Run frontend verification**

```powershell
npm run build
npm test
npm run test:e2e
```

- [x] **Step 8: Commit**

```powershell
git add src/Dotisan.Generators/Templates/Web tests
git commit -m "test: cover generated portal workflows"
```

---

## Phase 3: Earn ecosystem trust

### Task 14: Make migrations unsurprising and documentation consistent

**Files:**
- Modify: `src/Dotisan.Cli/BuiltInCommands.cs`
- Modify generated project README templates.
- Modify: `README.md`
- Modify: `docs/quickstart.md`
- Modify: `project.md`
- Modify: `docs/v086-acceptance.md`
- Add documentation consistency tests under `tests/Dotisan.Cli.Tests/DocumentationTests.cs`.

**Interfaces:**
- `dotisan new` generates files and optionally restores dependencies; it does not start Docker, author migrations, or mutate a database.
- `dotisan migrate` applies committed migrations only.
- Native `dotnet ef migrations add` remains the authoring command.

- [x] **Step 1: Add tests proving `new` never calls `docker compose up`, `dotnet ef migrations add`, or `dotnet ef database update`**

- [x] **Step 2: Remove migration creation/application from `NewCommand`**

- [x] **Step 3: Print provider-specific native next steps after generation**

- [x] **Step 4: Make every migration statement in the four documentation sources identical in meaning**

- [x] **Step 5: Add a documentation test that fails on the retired claims `creates and applies the initial migration` and `creates and applies the initial Identity schema`**

- [x] **Step 6: Commit**

```powershell
git add src/Dotisan.Cli README.md project.md docs tests
git commit -m "fix: make migration workflow explicit and consistent"
```

### Task 15: Make SDK, JavaScript, Docker, and package outputs reproducible

**Files:**
- Modify generated `global.json`, package manifests, lockfile strategy, Dockerfile, and Compose templates.
- Modify: `src/Dotisan.Cli/Dotisan.Cli.csproj`
- Modify: `Directory.Build.props`
- Modify: `.github/workflows/ci.yml`

**Interfaces:**
- Generated `global.json`: pinned .NET 10 feature band with `rollForward: latestFeature`.
- Docker builds use `npm ci` or `pnpm install --frozen-lockfile`.
- Container images use an explicit version or digest, never `latest`.

- [x] **Step 1: Add golden assertions for SDK roll-forward, lockfiles, frozen installs, and pinned Compose images**

- [x] **Step 2: Decide lockfile production inside the generator and produce the selected package manager's lockfile before reporting successful project creation**

- [x] **Step 3: Update Dockerfile templates to copy the exact lockfile and use frozen installation**

- [x] **Step 4: Replace `mcr.microsoft.com/dotnet/aspire-dashboard:latest` with a reviewed version or digest**

- [x] **Step 5: Derive CLI, NuGet, frontend-template, and release versions from one MSBuild property**

```xml
<PropertyGroup>
  <VersionPrefix>0.9.0</VersionPrefix>
  <ContinuousIntegrationBuild Condition="'$(CI)' == 'true'">true</ContinuousIntegrationBuild>
  <PublishRepositoryUrl>true</PublishRepositoryUrl>
  <EmbedUntrackedSources>true</EmbedUntrackedSources>
  <IncludeSymbols>true</IncludeSymbols>
  <SymbolPackageFormat>snupkg</SymbolPackageFormat>
</PropertyGroup>
```

- [x] **Step 6: Make the release job derive the `.nupkg` path from the tag/version and reject mismatches**

- [x] **Step 7: Run pack and inspect both `.nupkg` and `.snupkg`**

```powershell
dotnet pack src\Dotisan.Cli\Dotisan.Cli.csproj -c Release -o artifacts
tar -tf artifacts\Dotisan.*.nupkg
```

- [x] **Step 8: Commit**

```powershell
git add src Directory.Build.props .github
git commit -m "build: make generated and published artifacts reproducible"
```

### Task 16: Add public project governance and final release gates

**Files:**
- Create: `LICENSE`
- Create: `CHANGELOG.md`
- Create: `CONTRIBUTING.md`
- Create: `SECURITY.md`
- Create: `SUPPORT.md`
- Create: `.github/ISSUE_TEMPLATE/bug.yml`
- Create: `.github/ISSUE_TEMPLATE/feature.yml`
- Modify: `src/Dotisan.Cli/Dotisan.Cli.csproj`
- Modify: `README.md`
- Modify: `.github/workflows/ci.yml`

**Interfaces:**
- NuGet package metadata includes authors, repository URL/type, project URL, tags, icon, readme, license, release notes, and symbols.
- Public support and security reporting boundaries are explicit.

- [x] **Step 1: Add the MIT license text matching `PackageLicenseExpression=MIT`**

- [x] **Step 2: Add contribution setup, test commands, template-change rules, generated-fixture rules, and pull-request expectations**

- [x] **Step 3: Add a changelog using Keep a Changelog headings and SemVer versions**

- [x] **Step 4: Populate complete NuGet metadata and inspect the resulting nuspec**

- [x] **Step 5: Add release gates that reject dirty generated output, stale contracts, tag/version mismatch, missing package metadata, vulnerable production dependencies, and failed golden paths**

- [x] **Step 6: Run the complete release candidate acceptance sequence**

```powershell
dotnet restore Dotisan.sln
dotnet build Dotisan.sln -c Release --no-restore --warnaserror
dotnet test Dotisan.sln -c Release --no-build --no-restore
pwsh scripts\Test-GeneratedProject.ps1 -Profile minimal -PackageManager npm
pwsh scripts\Test-GeneratedProject.ps1 -Profile identity -PackageManager pnpm
pwsh scripts\Test-GeneratedProject.ps1 -Profile saas -PackageManager npm
pwsh scripts\Test-GeneratedProject.ps1 -Profile maximal -PackageManager pnpm
dotnet pack src\Dotisan.Cli\Dotisan.Cli.csproj -c Release --no-build -o artifacts
git diff --check
git status --short
```

Expected: every command succeeds; only explicitly expected artifact files appear in `git status`.

- [x] **Step 7: Perform a clean-machine smoke test**

Install the packed global tool into an empty tool path, generate a project outside the repository, remove access to the Dotisan source tree, then restore, build, test, generate a resource, regenerate contracts, build the frontend, build the container, and start it.

- [ ] **Step 8: Publish a prerelease package before stable**

Publish `0.9.0-beta.1`, collect external feedback using the issue templates, and promote only after the golden-path and clean-machine gates pass unchanged.

- [x] **Step 9: Commit**

```powershell
git add LICENSE CHANGELOG.md CONTRIBUTING.md SECURITY.md SUPPORT.md .github README.md src/Dotisan.Cli
git commit -m "docs: prepare Dotisan for public contribution and release"
```

---

## Final acceptance checklist

- [x] `dotisan new` has no undocumented side effects.
- [x] Minimal projects contain only dependencies required by the minimal profile.
- [x] A fresh project builds and tests with both npm and pnpm.
- [x] `make resource Customer` produces a compiling API feature.
- [x] `generate` adds Customer operations and models to every derived artifact.
- [x] `generate --check` succeeds when clean and fails without modifying files when stale.
- [x] Failed generation does not modify any previously valid artifact.
- [x] All generated mutation endpoints have appropriate authorization and antiforgery behavior.
- [x] All enabled integrations meet their documented readiness label.
- [x] Generated API, frontend, container, and migration instructions work outside the repository.
- [x] CI executes the same commands documented for users.
- [x] NuGet package metadata and symbols are complete.
- [x] Documentation contains one consistent description of contracts, migrations, profiles, and integration readiness.
- [x] Repository tests, generated-project tests, browser tests, package inspection, dependency audits, and clean-machine smoke tests all pass.

## Self-review record

- Spec coverage: all findings from the accepted assessment map to Tasks 1-16.
- Placeholder scan: no incomplete implementation placeholders are intentionally left in this plan.
- Type consistency: contract acquisition produces `ContractManifest`; renderers produce `GeneratedArtifact`; publication accepts only complete validated artifact sets.
- Scope: work is separated into stabilization, normalization, hardening, and ecosystem trust phases, each with independent review gates.
