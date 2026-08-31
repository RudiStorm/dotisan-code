# Task 2 Report: Deterministic OpenAPI Generation

Date: 2026-08-31
Branch: `v0.5-release`
Commit target: `feat: add deterministic OpenAPI contract renderer`

## Scope

Implemented the v0.5 Task 2 slice in `D:\dev\dotisan-code\.worktrees\v050-release` by adding a new `Dotisan.OpenApi` library, a focused `Dotisan.OpenApi.Tests` project, deterministic OpenAPI 3.1 rendering over `ContractManifest`, solution wiring, and a report artifact for the release handoff.

## Files Changed

- Added `src/Dotisan.OpenApi/Dotisan.OpenApi.csproj`
- Added `src/Dotisan.OpenApi/OpenApiDocumentGenerator.cs`
- Added `tests/Dotisan.OpenApi.Tests/Dotisan.OpenApi.Tests.csproj`
- Added `tests/Dotisan.OpenApi.Tests/OpenApiDocumentGeneratorTests.cs`
- Modified `Dotisan.sln`

## Task 1 Contract Metadata Consumption

The implementation consumes the reviewed transport metadata introduced by Task 1 (`b3cb6e0`, `2b45ca1`) by reading:

- `ContractManifest.EndpointMetadata` for request body, path/query parameters, success status code, and tags
- `ContractModel.Name` for OpenAPI schema names
- `ContractModel.SourceType` to resolve endpoint response models from `EndpointManifestEntry.Response`
- `ContractProperty.Nullable` and `ContractProperty.Optional` independently when rendering OpenAPI schema nullability and `required` lists

No reflection or external OpenAPI runtime dependency was added.

## TDD Notes

### RED

1. Added `tests/Dotisan.OpenApi.Tests` with a hand-built manifest fixture and exact assertions for:
   - path emission
   - operation IDs
   - request bodies
   - path/query parameters
   - component schemas
   - required property lists
   - numeric enum values and `x-enumNames`
   - deterministic JSON ordering
   - deterministic SHA-256
   - unsupported type failures
2. Ran `dotnet test tests\Dotisan.OpenApi.Tests`
3. Observed expected failure because `OpenApiDocumentGenerator` and the `Dotisan.OpenApi` project did not yet exist

### GREEN

Implemented a small `System.Text.Json`-backed renderer that:

- emits OpenAPI `3.1.0`
- writes deterministic `paths` and `components.schemas`
- maps contract primitives, arrays, dictionaries, objects, enums, and date/time formats
- distinguishes nullable versus optional properties
- resolves response schemas from Task 1 `SourceType` metadata
- returns JSON plus SHA-256 as `OpenApiDocumentResult`
- throws actionable errors for unsupported `Unknown` contract kinds

### REFACTOR

Kept the implementation as a single focused renderer file with small internal OpenAPI data records so the write order stays explicit and deterministic.

## Verification

### Focused project test

Command:

```powershell
dotnet test tests\Dotisan.OpenApi.Tests
```

Result:

- Initial red run failed because the library project and generator were absent
- After implementation and fixture correction, final run passed: `Passed! - Failed: 0, Passed: 2, Skipped: 0, Total: 2`

### Focused relevant regression coverage

Commands:

```powershell
dotnet test tests\Dotisan.OpenApi.Tests --no-restore
dotnet test tests\Dotisan.Core.Tests --no-restore
dotnet test Dotisan.sln --no-restore --filter "FullyQualifiedName~ContractManifestTests|FullyQualifiedName~OpenApiDocumentGeneratorTests"
```

Results:

- `Dotisan.OpenApi.Tests`: `Passed! - Failed: 0, Passed: 2, Skipped: 0, Total: 2`
- `Dotisan.Core.Tests`: `Passed! - Failed: 0, Passed: 21, Skipped: 0, Total: 21`
- `Dotisan.sln` filtered run:
  - `Dotisan.Core.Tests`: `Passed! - Failed: 0, Passed: 11, Skipped: 0, Total: 11`
  - `Dotisan.OpenApi.Tests`: `Passed! - Failed: 0, Passed: 2, Skipped: 0, Total: 2`
  - Other test projects reported no matches for the focused filter, which is expected for this run

## Notes and Limitations

- The sandbox blocked NuGet network access for the new test project during early verification, so focused `dotnet test` runs that required restore were rerun unsandboxed to obtain the real compile/test result.
- The current renderer always emits `"Success"` as the success response description and emits `application/json` content for non-204 responses. That stays within Task 2 scope and can be enriched later if a subsequent task introduces richer response metadata.

## Ready For Commit

The intended staged set is:

- `Dotisan.sln`
- `src/Dotisan.OpenApi/Dotisan.OpenApi.csproj`
- `src/Dotisan.OpenApi/OpenApiDocumentGenerator.cs`
- `tests/Dotisan.OpenApi.Tests/Dotisan.OpenApi.Tests.csproj`
- `tests/Dotisan.OpenApi.Tests/OpenApiDocumentGeneratorTests.cs`
- `.superpowers/sdd/2026-08-28-v050-release-completion/task-2-report.md`

## Fix Round 1 - 2026-08-31

### Review Findings Addressed

1. Optional route parameters are no longer silently forced to required OpenAPI path parameters. The generator now explicitly rejects optional route metadata with an actionable `InvalidOperationException`, which matches OpenAPI's requirement that path parameters be required.
2. Added focused regression coverage for `204 No Content` responses and verified the generated response omits the `content` object entirely.

### TDD Cycle

#### RED

Added two focused tests in `tests/Dotisan.OpenApi.Tests/OpenApiDocumentGeneratorTests.cs`:

- `Rejects_optional_route_parameters_with_actionable_error`
- `Omits_response_content_for_204_no_content_operations`

Ran:

```powershell
dotnet test tests\Dotisan.OpenApi.Tests --no-restore
```

Observed red result:

- `Rejects_optional_route_parameters_with_actionable_error` failed with `Assert.Throws() Failure: No exception was thrown`
- `Omits_response_content_for_204_no_content_operations` already passed, confirming the no-content branch was present but previously unguarded by a dedicated regression test

#### GREEN

Updated `src/Dotisan.OpenApi/OpenApiDocumentGenerator.cs` so path parameters flow through a dedicated validator that throws when `EndpointParameterMetadata.Optional` is `true` for a route parameter.

#### VERIFY

Ran:

```powershell
dotnet test tests\Dotisan.OpenApi.Tests --no-restore
dotnet test Dotisan.sln --no-restore --filter "FullyQualifiedName~OpenApiDocumentGeneratorTests|FullyQualifiedName~ContractManifestTests"
```

Results:

- `Dotisan.OpenApi.Tests`: `Passed! - Failed: 0, Passed: 4, Skipped: 0, Total: 4`
- `Dotisan.sln` filtered run:
  - `Dotisan.Core.Tests`: `Passed! - Failed: 0, Passed: 11, Skipped: 0, Total: 11`
  - `Dotisan.OpenApi.Tests`: `Passed! - Failed: 0, Passed: 4, Skipped: 0, Total: 4`
  - other test projects reported expected `No test matches` lines for the focused filter
