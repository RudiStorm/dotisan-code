# Task 2 Implementation Report

Task: v0.5 compile-time contract metadata foundation, Task 2 only.

## Scope Completed

Implemented Roslyn-based request/response contract extraction in `src/Dotisan.SourceGenerators/EndpointRegistrationGenerator.cs` and expanded focused coverage in `tests/Dotisan.SourceGenerators.Tests/SourceGeneratorTests.cs`.

The implementation:

- Preserves the existing endpoint discovery, endpoint manifest ordering, and generated class name `DotisanGeneratedEndpointExtensions`.
- Adds generated `ContractManifest`, `ContractManifestJson`, and `ContractManifestSha256` members that use Task 1’s Core API with schema version `1`.
- Extracts request/response metadata from nested `Request` and `Response` symbols at compile time with Roslyn only.
- Maps primitive, numeric, temporal, array, dictionary, object, enum, and unsupported shapes to the required contract descriptor kinds.
- Keeps nullability and optionality separate: nullability comes from Roslyn nullable state / `Nullable<T>`, while optionality comes only from `JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)`.
- Honors transport names from `JsonPropertyName` overrides and otherwise emits camel-case transport names.
- Emits inspectable `ContractModel[]` object expressions with deterministic model names, resolved reference names, sorted properties, and sorted enum values.
- Falls back to record primary-constructor syntax inspection for `JsonPropertyName` / `JsonIgnore(WhenWritingDefault)` when symbol-level attribute discovery does not surface those property-targeted attributes directly.
- Avoids runtime reflection, assembly loading, `GetTypes`, and `Activator`.

## TDD Evidence

### RED

Extended `Generates_sorted_explicit_mapping_and_manifest` first with a richer fixture covering:

- `JsonPropertyName("display_name")`
- `JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)`
- nullable value types
- nested object models
- enum models with explicit numeric values
- array and dictionary descriptors
- reversed declaration order determinism for the generated contract members

Initial focused generator test run failed as expected because the generated source did not yet expose contract metadata:

```text
Expected generated source to contain the ContractManifest member.
```

### GREEN

Implemented contract-model collection and emission inside the existing generator, including:

- a testable `DescribeType(ITypeSymbol type, string sourceTypeName, Action<ContractModel> addModel)` helper,
- recursive model discovery for nested object and enum types,
- deterministic model-name resolution with ordinal collision suffixing,
- generated `ContractManifest` / JSON / SHA-256 members,
- syntax-backed attribute fallback for record primary-constructor property targets.

During the green cycle, two integration issues surfaced and were fixed:

- `System.Text.Json` naming APIs were not available in the `netstandard2.0` generator project, so a local deterministic camel-case helper replaced that runtime dependency.
- Roslyn did not reliably surface property-targeted serialization attributes from the record primary-constructor fixture via symbol APIs alone, so compile-time syntax inspection was added as a fallback for those specific attributes.

### VERIFY

Focused red/green test:

```powershell
dotnet test tests\Dotisan.SourceGenerators.Tests\Dotisan.SourceGenerators.Tests.csproj --configuration Release --no-restore --filter FullyQualifiedName~Generates_sorted_explicit_mapping_and_manifest
```

Final result:

```text
Passed!  - Failed: 0, Passed: 1, Skipped: 0, Total: 1
```

Required generator verification:

```powershell
dotnet test tests\Dotisan.SourceGenerators.Tests\Dotisan.SourceGenerators.Tests.csproj --configuration Release --no-restore --filter FullyQualifiedName~SourceGeneratorTests
dotnet build src\Dotisan.SourceGenerators\Dotisan.SourceGenerators.csproj --configuration Release --no-restore --verbosity minimal
```

Final results:

```text
Passed!  - Failed: 0, Passed: 2, Skipped: 0, Total: 2
Build succeeded.
0 Warning(s)
0 Error(s)
```

## Self-Review Notes

Checked the final diff for:

- no changes to endpoint registration or endpoint manifest ordering behavior,
- generated output remaining inspectable object expressions rather than runtime inspection,
- request/response and nested model discovery staying compile-time authoritative,
- transport names honoring explicit overrides and otherwise using camel-case,
- nullability and optionality remaining separate signals,
- unsupported shapes still mapping to `Unknown`,
- deterministic model / property / enum ordering and reversed-endpoint stability,
- no reflection or assembly loading APIs introduced.

No remaining concerns after the focused source-generator tests and generator build completed successfully.
