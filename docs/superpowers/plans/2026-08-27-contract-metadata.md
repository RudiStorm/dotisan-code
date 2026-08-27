# v0.5 Compile-Time Contract Metadata Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a deterministic, compile-time request/response contract manifest and an in-memory TypeScript model renderer that can consume it without runtime reflection.

**Architecture:** `Dotisan.Core` owns immutable transport metadata and deterministic JSON/hash serialization. `Dotisan.SourceGenerators` inspects endpoint request/response symbols with Roslyn and emits ordinary C# manifest objects next to the existing endpoint registration and endpoint manifest. `Dotisan.TypeScript` consumes the Core manifest and renders deterministic `models.ts` text; no CLI, template, OpenAPI, or runtime discovery is added in this slice.

**Tech Stack:** .NET 8, C# records, `System.Text.Json`, SHA-256, Roslyn incremental source-generator APIs already used by the repository, xUnit, and TypeScript text generation.

**Spec:** `docs/superpowers/specs/2026-08-27-contract-metadata-design.md`

## Global Constraints

- Keep the existing endpoint manifest schema version at `1` and set the contract manifest schema version to `1`.
- The source generator remains the authority for discovery; no runtime reflection, assembly loading, or endpoint registry is introduced.
- Preserve standard ASP.NET Core, DI, EF Core, and configuration usage; this slice changes none of those foundations.
- Use `System.Text.Json` camel-case serialization and `[JsonPropertyName]` overrides for transport property names.
- Sort models, properties, enum values, and generated TypeScript declarations with ordinal comparisons before serialization/rendering.
- Represent unsupported CLR shapes as `unknown` metadata and `unknown` TypeScript rather than guessing a primitive.
- Use `ReferenceName` for object/enum model references and `ElementType` for array elements/dictionary values.
- Keep generated source inspectable in normal compiler output and never emit assembly-qualified names.
- The TypeScript renderer returns deterministic files in memory and does not write to disk in this slice.
- Existing CLI, generator, ASP.NET Core, endpoint registration, and generated-project tests must continue to pass unchanged unless a test explicitly adds coverage for this contract slice.

## File Map

- `src/Dotisan.Core/ContractManifest.cs` — public contract metadata records, type descriptors, normalization, JSON serialization, and SHA-256.
- `tests/Dotisan.Core.Tests/ContractManifestTests.cs` — Core ordering, validation, JSON shape, and hash tests.
- `src/Dotisan.SourceGenerators/EndpointRegistrationGenerator.cs` — Roslyn extraction of endpoint request/response models and emission of contract manifest members alongside the existing `DotisanGeneratedEndpointExtensions` members.
- `tests/Dotisan.SourceGenerators.Tests/SourceGeneratorTests.cs` — compiler-level extraction and inspectable generated-source assertions.
- `src/Dotisan.TypeScript/TypeScriptTypeMapper.cs` — descriptor-to-TypeScript type mapping while retaining the existing compatibility overload.
- `src/Dotisan.TypeScript/TypeScriptContractGenerator.cs` — deterministic `models.ts` renderer and generated-file result type.
- `tests/Dotisan.AspNetCore.Tests/TypeScriptTypeMapperTests.cs` — supported type mappings and complete model rendering assertions through the existing TypeScript test boundary.
- `README.md` and `docs/quickstart.md` — v0.5 foundation behavior, current output contract, and explicit non-goals.

---

### Task 1: Add the Core contract metadata model

**Files:**
- Create: `src/Dotisan.Core/ContractManifest.cs`
- Create: `tests/Dotisan.Core.Tests/ContractManifestTests.cs`

**Interfaces:**
- Consumes: existing `EndpointManifestEntry` from `Dotisan.Core`.
- Produces: `ContractManifest`, `ContractModel`, `ContractProperty`, `ContractEnumValue`, `ContractTypeKind`, `ContractTypeDescriptor`, `ContractManifest.ToJson()`, and `ContractManifest.Sha256` for Tasks 2 and 3.

- [ ] **Step 1: Write the failing Core tests**

Add tests that construct the public model directly and assert the exact stable behavior:

```csharp
[Fact]
public void Orders_models_properties_and_enum_values_and_serializes_camel_case()
{
    var manifest = new ContractManifest(
        1,
        [],
        [
            new ContractModel("ZModel", "global::Z", [
                new ContractProperty("zValue", new ContractTypeDescriptor(ContractTypeKind.Integer), false, false),
                new ContractProperty("aValue", new ContractTypeDescriptor(ContractTypeKind.String), true, true)
            ], []),
            new ContractModel("AEnum", "global::AEnum", [], [
                new ContractEnumValue("Second", 2),
                new ContractEnumValue("First", 1)
            ])
        ]);

    Assert.Equal(["AEnum", "ZModel"], manifest.Models.Select(model => model.Name));
    Assert.Equal(["aValue", "zValue"], manifest.Models[1].Properties.Select(property => property.Name));
    Assert.Equal(["First", "Second"], manifest.Models[0].EnumValues.Select(value => value.Name));
    Assert.Contains("\"schemaVersion\":1", manifest.ToJson());
    Assert.Contains("\"enumValues\":[{\"name\":\"First\",\"value\":1}", manifest.ToJson());
}

[Fact]
public void Hash_is_stable_for_equivalent_input_order()
{
    var first = CreateManifest(modelsInReverseOrder: false);
    var second = CreateManifest(modelsInReverseOrder: true);

    Assert.Equal(first.ToJson(), second.ToJson());
    Assert.Equal(first.Sha256, second.Sha256);
}

[Fact]
public void Rejects_invalid_descriptor_references()
{
    Assert.Throws<ArgumentException>(() => new ContractTypeDescriptor(ContractTypeKind.Object));
    Assert.Throws<ArgumentException>(() => new ContractTypeDescriptor(
        ContractTypeKind.Array,
        ElementType: null));
}
```

Include cases for a dictionary with a value descriptor, a valid object/enum `ReferenceName`, and the combined `Nullable = true`/`Optional = true` flags. Assert the JSON contains `type.kind`, `referenceName`, `elementType`, `nullable`, and `optional` in camel case.

- [ ] **Step 2: Run the focused tests and verify they fail**

Run:

```powershell
dotnet test tests\Dotisan.Core.Tests\Dotisan.Core.Tests.csproj --configuration Release --no-restore --filter FullyQualifiedName~ContractManifest
```

Expected: compile failure because the contract metadata types do not exist yet.

- [ ] **Step 3: Implement the immutable model and deterministic serialization**

Define the exact public types from the design:

```csharp
public sealed record ContractManifest(
    int SchemaVersion,
    IReadOnlyList<EndpointManifestEntry> Endpoints,
    IReadOnlyList<ContractModel> Models);

public sealed record ContractModel(
    string Name,
    string SourceType,
    IReadOnlyList<ContractProperty> Properties,
    IReadOnlyList<ContractEnumValue> EnumValues);

public sealed record ContractProperty(
    string Name,
    ContractTypeDescriptor Type,
    bool Nullable,
    bool Optional);

public sealed record ContractEnumValue(string Name, int Value);

public enum ContractTypeKind
{
    String, Boolean, Integer, Decimal, Guid, DateTime, DateOnly, TimeOnly,
    Array, Dictionary, Object, Enum, Unknown
}

public sealed record ContractTypeDescriptor(
    ContractTypeKind Kind,
    string? ReferenceName = null,
    ContractTypeDescriptor? ElementType = null);
```

Normalize the constructor inputs into arrays: sort endpoints with the existing endpoint ordering, models by `Name`, properties by `Name`, and enum values by `Name`. Validate non-empty model/property/enum names, require `ReferenceName` only for `Object`/`Enum`, and require `ElementType` only for `Array`/`Dictionary`; permit null reference/element data for primitive and `Unknown`. Serialize with `JsonNamingPolicy.CamelCase`, `DefaultIgnoreCondition = Never`, compact output, and lowercase SHA-256 over UTF-8 JSON, matching `EndpointManifest`.

- [ ] **Step 4: Run the focused tests and the Core project build**

Run:

```powershell
dotnet test tests\Dotisan.Core.Tests\Dotisan.Core.Tests.csproj --configuration Release --no-restore --filter FullyQualifiedName~ContractManifest
dotnet build src\Dotisan.Core\Dotisan.Core.csproj --configuration Release --no-restore --verbosity minimal
```

Expected: all contract tests pass and the Core build reports 0 warnings and 0 errors.

- [ ] **Step 5: Commit the Core slice**

```powershell
git add src\Dotisan.Core\ContractManifest.cs tests\Dotisan.Core.Tests\ContractManifestTests.cs
git commit -m "feat: add contract metadata model"
```

### Task 2: Extract request/response metadata in the source generator

**Files:**
- Modify: `src/Dotisan.SourceGenerators/EndpointRegistrationGenerator.cs`
- Modify: `tests/Dotisan.SourceGenerators.Tests/SourceGeneratorTests.cs`

**Interfaces:**
- Consumes: `ContractManifest` types from Task 1 and existing `IDotisanEndpoint`, `EndpointOptions`, and generated endpoint definitions.
- Produces: generated `ContractManifest ContractManifest`, `string ContractManifestJson`, and `string ContractManifestSha256` members on `DotisanGeneratedEndpoints`.

- [ ] **Step 1: Add a failing generator fixture and assertions**

Extend `Generates_sorted_explicit_mapping_and_manifest` with a fixture containing the following transport shapes:

```csharp
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Dotisan.Core;
using Microsoft.AspNetCore.Routing;

public sealed class AEndpoint : IDotisanEndpoint
{
    public sealed record Request(
        [property: JsonPropertyName("display_name")] string DisplayName,
        int? Count,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool Enabled,
        IReadOnlyList<Nested> Items,
        Dictionary<string, Status> Lookup);

    public sealed record Response(Nested Item);
    public sealed record Nested(Guid Id, DateOnly Date);
    public enum Status { Unknown = 0, Ready = 2 }
    public sealed class Handler { }
    public sealed class Validator { }
    public static EndpointOptions Configure() => new("a.read", "A", "ReadA", "GET", "/api/a");
    public static void Map(IEndpointRouteBuilder endpoints) { }
}
```

Assert the generated source contains `ContractManifestJson`, `ContractManifestSha256`, `ContractTypeKind.Integer`, `ContractTypeKind.Array`, `ContractTypeKind.Dictionary`, `ContractTypeKind.Object`, and `ContractTypeKind.Enum`; contains `display_name`; contains `nullable: true` and `optional: true`; contains deterministic references for `AEndpointNested` and `AEndpointStatus`; and includes enum values `Unknown`/`0` and `Ready`/`2`. Run the same fixture with endpoint declarations reversed and assert the generated contract member text is identical. Keep the existing no-reflection assertions.

- [ ] **Step 2: Run the focused generator test and verify it fails**

Run:

```powershell
dotnet test tests\Dotisan.SourceGenerators.Tests\Dotisan.SourceGenerators.Tests.csproj --configuration Release --no-restore --filter FullyQualifiedName~Generates_sorted_explicit_mapping_and_manifest
```

Expected: FAIL because the generated source does not yet expose contract metadata.

- [ ] **Step 3: Implement symbol-to-descriptor extraction**

In the existing generator, retain the endpoint discovery and mapping code, then collect each endpoint's nested `Request` and `Response` symbols. For every public instance property, derive the transport name from `JsonPropertyNameAttribute` or the ASP.NET Core `System.Text.Json` camel-case convention. Derive nullability from `NullableAnnotation.Annotated` and `Nullable<T>`; derive optionality only from `JsonIgnoreAttribute` with `Condition = JsonIgnoreCondition.WhenWritingDefault`.

Use a helper with this exact shape so the generator boundary stays testable:

```csharp
private static ContractTypeDescriptor DescribeType(
    ITypeSymbol type,
    string sourceTypeName,
    Action<ContractModel> addModel);
```

Map `string`, `bool`, integral numeric special types to `Integer`, floating/decimal types to `Decimal`, `Guid`, `DateTime`/`DateTimeOffset`, `DateOnly`, and `TimeOnly`. Map one-dimensional arrays and `IEnumerable<T>` to `Array`; map `Dictionary<string, T>` and `IDictionary<string, T>` to `Dictionary`; map enums to `Enum` and record their numeric constant values; map classes/records to `Object`; and map all other shapes to `Unknown`. Use `ReferenceName` for object/enum descriptors and `ElementType` for array/dictionary descriptors.

Name root models `<EndpointClassName>Request` and `<EndpointClassName>Response`. Name nested models from a sanitized fully qualified source type name, keeping the source name in `SourceType`; resolve identifier collisions by ordinal source-name order and append `2`, `3`, and so on. Sort all generated model/property/enum data before emitting it.

Emit ordinary C# object expressions in the generated class rather than runtime inspection. The emitted shape is:

```csharp
public static global::Dotisan.Core.ContractManifest ContractManifest { get; } =
    new global::Dotisan.Core.ContractManifest(
        1,
        EndpointManifest,
        global::System.Array.Empty<global::Dotisan.Core.ContractModel>());

public static string ContractManifestJson => ContractManifest.ToJson();
public static string ContractManifestSha256 => ContractManifest.Sha256;
```

For a non-empty endpoint assembly, replace the empty model array with the generated `ContractModel[]` expressions, one model per discovered request, response, nested object, or enum type; the public property names and accessor expressions remain exactly the same.

Escape generated strings with a dedicated C# string-literal helper. Do not use `Assembly.Load`, `GetTypes`, `Activator`, or reflection APIs.

- [ ] **Step 4: Run the focused generator tests and inspect generated output**

Run:

```powershell
dotnet test tests\Dotisan.SourceGenerators.Tests\Dotisan.SourceGenerators.Tests.csproj --configuration Release --no-restore --filter FullyQualifiedName~SourceGeneratorTests
dotnet build src\Dotisan.SourceGenerators\Dotisan.SourceGenerators.csproj --configuration Release --no-restore --verbosity minimal
```

Expected: all source-generator tests pass, generated source contains the asserted contract members, and the analyzer build reports 0 warnings and 0 errors.

- [ ] **Step 5: Commit the generator slice**

```powershell
git add src\Dotisan.SourceGenerators\EndpointRegistrationGenerator.cs tests\Dotisan.SourceGenerators.Tests\SourceGeneratorTests.cs
git commit -m "feat: generate compile-time contract metadata"
```

### Task 3: Render the Core manifest as deterministic TypeScript models

**Files:**
- Modify: `src/Dotisan.TypeScript/TypeScriptTypeMapper.cs`
- Create: `src/Dotisan.TypeScript/TypeScriptContractGenerator.cs`
- Modify: `tests/Dotisan.AspNetCore.Tests/TypeScriptTypeMapperTests.cs`

**Interfaces:**
- Consumes: `ContractManifest` and `ContractTypeDescriptor` from Task 1.
- Produces: `GeneratedTypeScriptFile(string Path, string Content)`, `TypeScriptTypeMapper.Map(ContractTypeDescriptor, bool nullable = false, bool optional = false)`, and `TypeScriptContractGenerator.Generate(ContractManifest)` returning `IReadOnlyList<GeneratedTypeScriptFile>`.

- [ ] **Step 1: Add failing mapper and renderer tests**

Add exact mapping tests:

```csharp
[Theory]
[InlineData(ContractTypeKind.String, "string")]
[InlineData(ContractTypeKind.Boolean, "boolean")]
[InlineData(ContractTypeKind.Integer, "number")]
[InlineData(ContractTypeKind.Decimal, "number")]
[InlineData(ContractTypeKind.Guid, "string")]
[InlineData(ContractTypeKind.DateTime, "string")]
[InlineData(ContractTypeKind.DateOnly, "string")]
[InlineData(ContractTypeKind.TimeOnly, "string")]
[InlineData(ContractTypeKind.Unknown, "unknown")]
public void Maps_primitive_contract_kinds(ContractTypeKind kind, string expected)
    => Assert.Equal(expected, TypeScriptTypeMapper.Map(new ContractTypeDescriptor(kind)));
```

Add descriptor tests for `Array(Integer)` => `number[]`, `Dictionary(String)` => `Record<string, string>`, and `Object("Profile")` => `Profile`; assert nullable/optional yields `string | null | undefined`. Add a renderer test that expects one `models.ts` file with numeric enums before interfaces and deterministic output such as:

```typescript
export enum Status {
  Unknown = 0,
  Ready = 2,
}

export interface Request {
  displayName: string;
  status: Status | null | undefined;
}
```

The fixture must include an array, dictionary, nested object reference, nullable property, and optional property. Generate from reversed model/property input and assert identical file content.

- [ ] **Step 2: Run the focused TypeScript tests and verify they fail**

Run:

```powershell
dotnet test tests\Dotisan.AspNetCore.Tests\Dotisan.AspNetCore.Tests.csproj --configuration Release --no-restore --filter FullyQualifiedName~TypeScriptTypeMapperTests
```

Expected: compile failure for the descriptor overload and renderer types, followed by assertion failures for the new cases once the signatures exist.

- [ ] **Step 3: Implement descriptor mapping and `models.ts` rendering**

Keep the existing `ContractType` overload as a compatibility wrapper that constructs a `ContractTypeDescriptor`. Add recursive mapping with these exact rules:

```text
String -> string       Boolean -> boolean      Integer/Decimal -> number
Guid/DateTime/DateOnly/TimeOnly -> string      Unknown -> unknown
Array(T) -> Map(T)[]   Dictionary(T) -> Record<string, Map(T)>
Object/Enum reference -> ReferenceName
```

Apply `| null` first when `nullable` is true, then `| undefined` when `optional` is true. Render enum models before object models, sort each group by model name, and emit `export enum` values with explicit numeric assignments. Render object properties with camel-case names as supplied by the manifest and the mapped union type; use `propertyName: Type;` so both nullability and optionality are visible in the generated transport contract. End the file with a newline and return `new GeneratedTypeScriptFile("models.ts", content)`.

Use a single StringBuilder and ordinal sorting so equal manifests always produce byte-identical output. Do not add npm or TypeScript runtime dependencies.

- [ ] **Step 4: Run TypeScript tests and build the package**

Run:

```powershell
dotnet test tests\Dotisan.AspNetCore.Tests\Dotisan.AspNetCore.Tests.csproj --configuration Release --no-restore --filter FullyQualifiedName~TypeScriptTypeMapperTests
dotnet build src\Dotisan.TypeScript\Dotisan.TypeScript.csproj --configuration Release --no-restore --verbosity minimal
```

Expected: all mapping/rendering tests pass and the package builds with 0 warnings and 0 errors.

- [ ] **Step 5: Commit the TypeScript slice**

```powershell
git add src\Dotisan.TypeScript\TypeScriptTypeMapper.cs src\Dotisan.TypeScript\TypeScriptContractGenerator.cs tests\Dotisan.AspNetCore.Tests\TypeScriptTypeMapperTests.cs
git commit -m "feat: render contract metadata as typescript"
```

### Task 4: Document the v0.5 foundation and run the repository gate

**Files:**
- Modify: `README.md`
- Modify: `docs/quickstart.md`

**Interfaces:**
- Consumes: the public generated members and renderer behavior from Tasks 1–3.
- Produces: user-facing documentation that accurately describes the available contract metadata and its explicit boundaries.

- [ ] **Step 1: Add documentation assertions to the existing documentation**

Document that an endpoint assembly with the source generator exposes `DotisanGeneratedEndpointExtensions.ContractManifest`, `DotisanGeneratedEndpointExtensions.ContractManifestJson`, and `DotisanGeneratedEndpointExtensions.ContractManifestSha256`; that the manifest contains deterministic request/response model metadata and can be passed to `TypeScriptContractGenerator.Generate`; and that `models.ts` is returned in memory. Include a compact example:

```csharp
var files = TypeScriptContractGenerator.Generate(DotisanGeneratedEndpointExtensions.ContractManifest);
File.WriteAllText(Path.Combine("src", "generated", files[0].Path), files[0].Content);
```

State plainly that OpenAPI, Zod, fetch clients, TanStack Query, Vue-template wiring, CLI commands, stale checks, validation-rule extraction, and CRUD scaffolding are not part of this slice.

- [ ] **Step 2: Review the docs for consistency with the design spec**

Run:

```powershell
rg -n "ContractManifest|models\.ts|OpenAPI|Zod|TanStack|stale|reflection|v0\.5" README.md docs\quickstart.md
```

Expected: both docs describe only the implemented compile-time metadata/renderer boundary and identify each excluded integration explicitly.

- [ ] **Step 3: Run the full verification gate**

Run:

```powershell
dotnet restore Dotisan.sln
dotnet build Dotisan.sln --configuration Release --no-restore --verbosity minimal
dotnet test Dotisan.sln --configuration Release --no-restore --verbosity minimal
dotnet run --project src\Dotisan.Cli\Dotisan.Cli.csproj -- --version
git diff --check
```

Expected: restore succeeds if NuGet is reachable; the solution build reports 0 warnings/errors; all existing and new tests pass; the CLI reports `dotisan 0.4.0` because this slice does not change the CLI release version; and `git diff --check` reports no whitespace errors. If restore or tests are blocked by environment/network state, record the exact command and full blocker text in the final handoff instead of claiming success.

- [ ] **Step 4: Commit the documentation and verification-ready slice**

```powershell
git add README.md docs\quickstart.md
git commit -m "docs: document v0.5 contract metadata foundation"
```

## Final self-review checklist

- [ ] Every requirement in `docs/superpowers/specs/2026-08-27-contract-metadata-design.md` is covered by Tasks 1–4.
- [ ] No task uses runtime discovery, reflection, assembly loading, a custom DI container, a custom ORM, or a second backend contract authority.
- [ ] All public types and method signatures consumed by later tasks exactly match the types produced by earlier tasks.
- [ ] Unsupported CLR shapes have an explicit `Unknown`/`unknown` path.
- [ ] The plan contains no unfinished placeholder markers or unspecified implementation step.
- [ ] Full solution verification is run before claiming completion.
