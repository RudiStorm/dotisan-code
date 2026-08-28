# Task 3 Report

Implemented Task 3 for the contract-metadata foundation: recursive Core descriptor mapping and deterministic `models.ts` rendering.

## What Changed

- Added `TypeScriptTypeMapper.Map(ContractTypeDescriptor, bool nullable = false, bool optional = false)`.
- Kept the existing `TypeScriptTypeMapper.Map(ContractType, ...)` overload as a compatibility wrapper.
- Added recursive mapping for:
  - primitives
  - arrays
  - dictionaries
  - object and enum references
  - unknown fallback
- Added `GeneratedTypeScriptFile(string Path, string Content)`.
- Added `TypeScriptContractGenerator.Generate(ContractManifest)` returning a single generated file named `models.ts`.
- Rendered enum models before object models.
- Rendered object and enum groups with ordinal model-name sorting.
- Rendered explicit numeric enum assignments.
- Preserved deterministic property ordering in generated interfaces.
- Preserved `nullable` then `optional` union order.

## Verification

Ran the focused tests from the brief:

```powershell
dotnet test tests\Dotisan.AspNetCore.Tests\Dotisan.AspNetCore.Tests.csproj --configuration Release --no-restore --filter FullyQualifiedName~TypeScriptTypeMapperTests
```

Result: passed, 12 tests total.

Ran the package build from the brief:

```powershell
dotnet build src\Dotisan.TypeScript\Dotisan.TypeScript.csproj --configuration Release --no-restore --verbosity minimal
```

Result: succeeded with 0 warnings and 0 errors.

## Self-Review

- The implementation stays within the existing package boundary and does not add runtime or npm dependencies.
- The renderer uses a single `StringBuilder` and stable ordinal sorting for deterministic output.
- The generated file ends with a newline.
- The compatibility overload remains available for any existing callers using `ContractType`.

## Notes

- Enum members are rendered in numeric order so the output matches the required deterministic sample (`Unknown = 0` before `Ready = 2`) while still remaining stable across manifest input order.

## Round 1 Fix

Addressed two review findings:

- `TypeScriptContractGenerator.Generate` now emits a newline-terminated `models.ts` even when the manifest has no models.
- Added focused coverage for the compatibility overload `TypeScriptTypeMapper.Map(ContractType, nullable, optional)` to confirm it still maps flags correctly.

Verification after the fix:

```powershell
dotnet test tests\Dotisan.AspNetCore.Tests\Dotisan.AspNetCore.Tests.csproj --configuration Release --no-restore --filter FullyQualifiedName~TypeScriptTypeMapperTests
dotnet build src\Dotisan.TypeScript\Dotisan.TypeScript.csproj --configuration Release --no-restore --verbosity minimal
```

Both commands passed with no warnings or errors on the build.

## Final Review Fix - 2026-08-28

Implemented the final-review fix for enum metadata ordering in the TypeScript renderer without widening production scope.

### Finding Addressed

- `src/Dotisan.TypeScript/TypeScriptContractGenerator.cs` was reordering enum members by numeric `Value` during rendering.
- The canonical contract manifest already normalizes enum members by ordinal `Name`.
- That extra renderer sort could make generated `models.ts` disagree with the manifest whenever numeric order and name order differ.

### Root Cause

The bug was in the renderer layer, not manifest construction:

- `ContractModel.EnumValues` is already normalized in `Dotisan.Core` with ordinal name ordering.
- `TypeScriptContractGenerator.AppendEnum(...)` applied a second sort:

```csharp
.OrderBy(enumValue => enumValue.Value)
.ThenBy(enumValue => enumValue.Name, StringComparer.Ordinal)
```

That logic overrode the canonical manifest order and produced value-sorted TypeScript enums.

### TDD Regression Coverage

Added a failing regression test first in `tests/Dotisan.AspNetCore.Tests/TypeScriptTypeMapperTests.cs`:

- `Renders_enum_members_in_manifest_name_order_instead_of_numeric_value_order`

The fixture uses:

- `Alpha = 20`
- `Beta = 10`

Expected output preserves manifest name order:

```typescript
export enum Priority {
  Alpha = 20,
  Beta = 10,
}
```

Red-phase verification:

```powershell
dotnet test tests\Dotisan.AspNetCore.Tests\Dotisan.AspNetCore.Tests.csproj --configuration Release --no-restore --filter FullyQualifiedName~TypeScriptTypeMapperTests
```

Observed failure was the intended one: actual output rendered `Beta` before `Alpha`, proving the renderer was value-sorting instead of preserving manifest order.

### Production Change

Made the minimal fix in `src/Dotisan.TypeScript/TypeScriptContractGenerator.cs`:

- removed the value/name re-sort in `AppendEnum(...)`
- now emits `model.EnumValues` in the canonical order supplied by the manifest

No other production files or behaviors were changed.

### Test Adjustment

Updated the existing deterministic renderer expectation in `TypeScriptTypeMapperTests.cs` to reflect canonical enum-name ordering for `Status`:

- `Ready = 2`
- `Unknown = 0`

This aligns the existing test with the contract metadata ordering rules already enforced by `ContractModel`.

### Verification

Focused TypeScript test suite:

```powershell
dotnet test tests\Dotisan.AspNetCore.Tests\Dotisan.AspNetCore.Tests.csproj --configuration Release --no-restore --filter FullyQualifiedName~TypeScriptTypeMapperTests
```

Result: passed, 15 tests total, 0 failed.

Focused package build:

```powershell
dotnet build src\Dotisan.TypeScript\Dotisan.TypeScript.csproj --configuration Release --no-restore --verbosity minimal
```

Result: succeeded with 0 warnings and 0 errors.

Additional hygiene check:

```powershell
git diff --check
```

Result: no whitespace errors; only line-ending normalization warnings from Git for the edited files.

### Deferred Minor Audit Note Review

Reviewed the already-deferred minor note about Task 4 reporting:

- `task-4-report.md` summarizes the first sandbox `dotnet restore` failure instead of preserving the full original blocker output.
- I did not expand this fix into Task 4 report rewrites or unrelated production changes, per instruction.
- The note remains a documentation/reporting concern rather than a production-code concern for this final-review fix.
