# Task 1 Report: Stabilize the v0.5 contract transport model

Date: 2026-08-28
Worktree: `D:\dev\dotisan-code\.worktrees\v050-release`
Branch: `v0.5-release`

## Summary

Implemented the Task 1 transport-metadata expansion in the existing compile-time contract pipeline without changing schema version `1`, runtime discovery behavior, or the legacy three-argument `ContractManifest` call sites.

The manifest now supports optional endpoint transport metadata alongside the existing endpoint and model collections. The source generator emits inspectable C# for endpoint transport metadata and wires it into the generated `ContractManifest`.

## Scope Completed

- Modified `src/Dotisan.Core/ContractManifest.cs`
- Modified `src/Dotisan.SourceGenerators/EndpointRegistrationGenerator.cs`
- Extended `tests/Dotisan.Core.Tests/ContractManifestTests.cs`
- Extended `tests/Dotisan.SourceGenerators.Tests/SourceGeneratorTests.cs`

## TDD Record

### RED

Added focused failing tests first:

- `ContractManifestTests` now expects:
  - legacy manifests to omit `endpointMetadata` when none is supplied
  - deterministic endpoint transport metadata sorting alongside endpoints
  - request-body/query/path/success-status/tag/validation serialization
  - route-token inference without request DTO path members
- `SourceGeneratorTests` now expects:
  - generated `EndpointMetadata`
  - emitted `EndpointContractMetadata.Create(...)` calls
  - generated request-body and request-parameter descriptors
  - deterministic generated metadata text when endpoint declarations are reversed

Observed RED results before implementation:

- Core tests failed to compile because `ContractManifest.EndpointMetadata`, `EndpointContractMetadata`, `EndpointRequestBodyMetadata`, and `EndpointParameterMetadata` did not exist.
- Source-generator tests failed because the generated source did not contain the new endpoint metadata member/output.

### GREEN

Implemented the minimal production changes to satisfy the failing tests, then reran the focused filters until green.

### REFACTOR / Tightening

- Kept the existing constructor pattern and deterministic ordering behavior.
- Preserved old JSON output for legacy `ContractManifest` callers by omitting `endpointMetadata` when the optional metadata argument is not supplied.
- Reused existing contract type-descriptor emission rather than adding a parallel transport-type system.

## Implementation Notes

### Core manifest model

`ContractManifest` now accepts an optional aligned `EndpointMetadata` collection. When present, endpoint metadata is reordered with the corresponding endpoint entries using the existing endpoint sort key (`Id`, `Method`, `Route`).

Added immutable transport records:

- `EndpointContractMetadata`
- `EndpointRequestBodyMetadata`
- `EndpointParameterMetadata`
- `EndpointValidationMetadata`
- `EndpointValidationRuleMetadata`

`EndpointContractMetadata.Create(...)` now derives:

- request-body presence from HTTP verb
- path parameters from route tokens
- path parameter types from matching request parameters or common ASP.NET route constraints
- query parameters from non-body request properties that are not route tokens
- success status code heuristics (`POST` => `201`, `DELETE` => `204`, otherwise `200`)
- deterministic tag normalization
- a portable validation envelope with `Enabled` plus an empty rules list for this task

### Source generator

The generator now emits:

- `public static IReadOnlyList<EndpointContractMetadata> EndpointMetadata { get; }`
- explicit `EndpointContractMetadata.Create(...)` calls in generated C#
- request-body type descriptors for nested `Request` types
- request-parameter descriptor arrays derived from the request transport properties
- `ContractManifest` construction that includes `EndpointMetadata`

Discovery remains compile-time/source-generator based. No reflection, custom DI, or external runtime dependency was introduced.

## Verification

Focused RED/GREEN cycle:

```powershell
dotnet test tests\Dotisan.Core.Tests\Dotisan.Core.Tests.csproj --no-restore --filter FullyQualifiedName~ContractManifest
dotnet test tests\Dotisan.SourceGenerators.Tests\Dotisan.SourceGenerators.Tests.csproj --no-restore --filter FullyQualifiedName~SourceGeneratorTests
```

Final verification runs:

```powershell
dotnet test tests\Dotisan.Core.Tests\Dotisan.Core.Tests.csproj --no-restore --no-build
dotnet test tests\Dotisan.SourceGenerators.Tests\Dotisan.SourceGenerators.Tests.csproj --no-restore --no-build
```

Results:

- `Dotisan.Core.Tests`: 20 passed, 0 failed
- `Dotisan.SourceGenerators.Tests`: 4 passed, 0 failed

## Self-Review

- Scope stayed within the briefed implementation files plus the required focused tests and report.
- Existing `EndpointManifestEntry` and schema version `1` remain intact.
- Existing `ContractManifest` callers keep their prior JSON/hash output unless they opt into endpoint metadata.
- Generated transport metadata is inspectable C# and deterministic across reversed endpoint declaration order.

## Concerns

- Success-status inference is intentionally minimal for Task 1: `POST` maps to `201`, `DELETE` maps to `204`, and other verbs currently map to `200`.
- Route-token type inference currently recognizes the common typed ASP.NET constraints (`guid`, `int`, `long`, `short`, `byte`, `bool`, `decimal`, `double`, `float`, `datetime`) and falls back to `string`.
- Portable validation rules are intentionally scaffolded as an enabled/rules envelope here; rule extraction remains for the later validation task.

## Fix Round 1 — 2026-08-31

### Review Findings Addressed

1. Restored a real public three-parameter `ContractManifest` constructor overload while keeping the endpoint-metadata overload.
2. Strengthened source-generator tests to compile the generated assembly, execute the exported manifest properties, and verify real `Json` and `Sha256` behavior rather than relying only on source-text assertions.
3. Changed generated `EndpointManifest` and `EndpointMetadata` to reuse one `Configure()` snapshot per endpoint so exported values cannot diverge at runtime.

### RED

- The uncommitted compatibility test failed because `ContractManifest` no longer exposed an exact three-parameter constructor.
- The new generated-behavior test failed because `SnapshotEndpoint.Configure()` was invoked `5` times instead of `1`.

### GREEN

- Added `public ContractManifest(int, IReadOnlyList<EndpointManifestEntry>, IReadOnlyList<ContractModel>)`.
- Emitted one generated `EndpointOptions` snapshot field per endpoint and routed both generated manifest exports through that field.
- Added a real generator execution test that:
  - compiles the generated source into an assembly
  - reads `EndpointManifest`, `EndpointMetadata`, and `ContractManifest`
  - verifies `EndpointManifestJson`, `EndpointManifestSha256`, `ContractManifestJson`, and `ContractManifestSha256`
  - proves one `Configure()` call per endpoint

### Verification

Focused:

```powershell
dotnet test tests\Dotisan.Core.Tests\Dotisan.Core.Tests.csproj --no-restore --filter FullyQualifiedName~ContractManifest
dotnet test tests\Dotisan.SourceGenerators.Tests\Dotisan.SourceGenerators.Tests.csproj --no-restore --filter FullyQualifiedName~SourceGeneratorTests
```

Full relevant suites:

```powershell
dotnet test tests\Dotisan.Core.Tests\Dotisan.Core.Tests.csproj --no-restore --no-build
dotnet test tests\Dotisan.SourceGenerators.Tests\Dotisan.SourceGenerators.Tests.csproj --no-restore --no-build
```

Results:

- `Dotisan.Core.Tests`: 21 passed, 0 failed
- `Dotisan.SourceGenerators.Tests`: 5 passed, 0 failed

### Scope Notes

- Added `tests/Dotisan.SourceGenerators.Tests/Dotisan.SourceGenerators.Tests.csproj` reference coverage for `Dotisan.AspNetCore` so the generated-assembly test can compile the emitted extension type on a clean build.
